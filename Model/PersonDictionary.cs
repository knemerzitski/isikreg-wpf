using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Extensions;
using IsikReg.IO;
using IsikReg.Json;
using IsikReg.Properties;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace IsikReg.Model {

  public enum PersonDictionaryAction {
    ADDED,
    REMOVED,
    CLEARED
  }

  public delegate void PersonDictionaryChangedEventHandler(PersonDictionaryAction action, Person? person = null);

  public class PersonDictionary : IDictionary<string, Person>, IReadOnlyDictionary<string, Person> {

    private static readonly Lazy<PersonDictionary> lazyInstance = new(() => {
      return new("./isikreg");
    });

    public static PersonDictionary Instance { get => lazyInstance.Value; }

    private readonly JsonSerializerOptions JSON_OPTIONS = new() {
      WriteIndented = true,
      TypeInfoResolver = new DefaultJsonTypeInfoResolver {
        Modifiers = {
          TypeInfoResolverModifiers.SkipEmptyCollections,
          TypeInfoResolverModifiers.SkipNullOrWhitespaceStrings,
          TypeInfoResolverModifiers.SkipNullProperties
        }
      },
      Converters = {
           new Person.Converter(),
           new ColumnStringDictionaryConverter<object>(),
        },
    };

    private readonly Dictionary<string, Person> map = new();

    public Property<int> CountProperty { get; } = new();

    public bool IsEmpty { get => map.Count == 0; }
    public int Size { get => map.Count; }

    public PersonDictionary(string namePath) {
      string? relDir = Path.GetDirectoryName(namePath);
      if (string.IsNullOrWhiteSpace(relDir)) {
        relDir = "./";
      }
      string dir = Path.GetFullPath(relDir);
      if (!Path.Exists(dir)) {
        throw new DirectoryNotFoundException($"Path '{dir}' doesn't exist!");
      }

      //listReadOnly = new(list);

      string name = Path.GetFileName(namePath);

      path = Path.Join(dir, name + ".json.zip");
      zipEntryName = name + ".json";

      Task.Factory.StartNew(() => SaveLoop(), TaskCreationOptions.LongRunning).OnExceptionQuit();

      pausedEvent.Signal(); // Not paused
      savingEvent.Signal(); // Not saving
    }

    ~PersonDictionary() {
      savingEvent.Dispose();
      pausedEvent.Dispose();
    }

    #region Methods

    public Person? Add(Person person) {
      if (!App.IsApplicationThread()) {
        queuedAddActions.Enqueue(new() { Person = person });
        return null;
      }
      if (string.IsNullOrWhiteSpace(person.PersonalCode)) return null;

      Person? existingPerson = this.GetValueOrDefault(person.PersonalCode);
      if (existingPerson == person) return existingPerson;

      if (existingPerson == null) {
        AddListeners(person);
        map[person.PersonalCode] = person;
        Added?.Invoke(person);
        existingPerson = person;
      } else {
        existingPerson.Merge(person);
      }

      existingPerson.UpdateAutoFillIndex();

      return existingPerson;
    }

    public Person? Add(IReadOnlyDictionary<string, object> personProperties, IEnumerable<IReadOnlyDictionary<string, object>>? registrationProperties = null) {
      if (!App.IsApplicationThread()) {
        queuedAddActions.Enqueue(new() { Properties = personProperties, RegistrationProperties = registrationProperties });
        return null;
      }

      string personalCode = personProperties.GetPersonalCode();
      if (string.IsNullOrWhiteSpace(personalCode)) return null;

      Person? existingPerson = this.GetValueOrDefault(personalCode);
      if (existingPerson == null) {
        Person person = new(personProperties, registrationProperties);
        AddListeners(person);
        map[person.PersonalCode] = person;
        Added?.Invoke(person);
        existingPerson = person;
      } else {
        existingPerson.Merge(personProperties, registrationProperties);
      }

      existingPerson.UpdateAutoFillIndex();

      return existingPerson;
    }

    private void UpdatePersonalCode(Person person, string? oldPersonalCode = null) {
      Person? existingPerson = !string.IsNullOrWhiteSpace(person.PersonalCode) ? this.GetValueOrDefault(person.PersonalCode) : null;
      if (existingPerson == person) return;

      if (existingPerson == null) {
        if (oldPersonalCode != null) {
          map.Remove(oldPersonalCode);
          DeleteSave(oldPersonalCode);
        }
        map[person.PersonalCode] = person;
        Save(person);
      } else {
        throw new Exception($"Duplicate personal code {person.PersonalCode}");
      }
    }

    private void AddListeners(Person person) {
      person.Properties.PropertyChanged += (key, oldValue, newValue, action) => {
        switch (Config.Instance.Columns.GetValueOrDefault(key)?.Id) {
          case ColumnId.PERSONAL_CODE:
            UpdatePersonalCode(person, oldValue as string);
            break;
        }
      };
    }

    public void Remove(Person person) {
      person.Remove(); // Triggers listeners by removing registrations
      map.Remove(person.PersonalCode);
      Removed?.Invoke(person);
      DeleteSave(person);
    }

    #endregion

    #region Events

    public event Action<Person>? Added;
    public event Action<Person>? Removed;
    public event Action? Cleared;

    #endregion

    #region Pausing

    private readonly CountdownEvent pausedEvent = new(1);
    public bool Paused {
      get => pausedEvent.CurrentCount != 0;
      set {
        if (Paused == value) return;

        if (value) {
          pausedEvent.Reset();
        } else {
          pausedEvent.Signal();

          if (App.IsApplicationThread()) {
            ProcessQueuedAddActions();
          }
        }
      }
    }

    #endregion

    #region Add Queueing

    private class AddAction {
      public Person? Person { get; init; } = null;

      public IReadOnlyDictionary<string, object>? Properties { get; init; } = null;
      public IEnumerable<IReadOnlyDictionary<string, object>>? RegistrationProperties { get; init; } = null;

    }

    private readonly ConcurrentQueue<AddAction> queuedAddActions = new();

    private void ProcessQueuedAddActions() {
      if (queuedAddActions.IsEmpty) return;
      while (queuedAddActions.TryDequeue(out AddAction? evt)) {
        if (evt.Person != null) {
          Add(evt.Person);
        } else if (evt.Properties != null) {
          Add(evt.Properties, evt.RegistrationProperties);
        }
      }
    }

    #endregion

    #region Serialization

    private record SaveItem {
      public string PersonalCode { get; }
      public SaveType SaveType { get; }
      public SaveItem(string personalCode, SaveType saveType) {
        PersonalCode = personalCode;
        SaveType = saveType;
      }
    }
    private enum SaveType {
      WRITE, DELETE
    }

    private readonly string path;
    private readonly string zipEntryName;

    private readonly Dictionary<string, JsonElement> jsonList = new();

    private readonly CountdownEvent savingEvent = new(1);
    private readonly BlockingCollection<SaveItem> saveItems = new();

    private void Delete() {
      SafeFileStream.Delete(path);
    }
    public void Read(Action? onStartReading = null, Action? onStopReading = null) {
      Task.Run(() => {
        try {
          onStartReading?.Invoke();
          ReadJsonZip();
          LoadFromJsonMap();
        } finally {
          onStopReading?.Invoke();
        }
      }).OnExceptionQuit();
    }

    private void ReadJsonZip() {
      if (!SafeFileStream.IsReadable(path)) return;
      using SafeFileStream fs = new(path, FileMode.Open, FileAccess.Read);
      using ZipArchive zip = new(fs);
      ZipArchiveEntry? entry = zip.GetEntry(zipEntryName);
      if (entry == null) {
        throw new FileNotFoundException($"Zip entry '{zipEntryName}' not found in {path}");
      }
      using Stream entryStream = entry.Open();

      jsonList.Clear();

      Dictionary<string, JsonElement>? newJsonList = (Dictionary<string, JsonElement>?)JsonSerializer.Deserialize(entryStream, typeof(Dictionary<string, JsonElement>), JSON_OPTIONS);
      if (newJsonList == null) return;
      foreach ((string personalCode, JsonElement element) in newJsonList) {
        jsonList[personalCode] = element;
      }
    }

    private void LoadFromJsonMap() {
      //List<Person> newList = new();

      foreach (JsonElement element in jsonList.Values) {
        Person? person = element.Deserialize<Person>(JSON_OPTIONS);
        if (person != null) {
          //person.CleanUpRegistrations();
          //newList.Add(person);
          Add(person);
        }
      }

      //App.Run(() => {
      //  newList.ForEach(x => Add(x));
      //});
    }

    public void Save(Person p) {
      Save(p.PersonalCode);
    }

    public void Save(string personalCode) {
      saveItems.Add(new SaveItem(personalCode, SaveType.WRITE));
    }

    private void DeleteSave(Person p) {
      DeleteSave(p.PersonalCode);
    }

    public void DeleteSave(string personalCode) {
      saveItems.Add(new SaveItem(personalCode, SaveType.DELETE));
    }

    public void WaitSaved() {
      savingEvent.Wait();
    }

    private void SaveLoop() {
      HashSet<string> processedCodes = new();
      Queue<SaveItem> uniqueSaveQueue = new();
      while (true) {
        processedCodes.Clear();
        uniqueSaveQueue.Clear();

        SaveItem firstItem = saveItems.Take(); // Blocking operation
        //App.WaitCycle();
        try {
          savingEvent.Reset();
          pausedEvent.Wait();
          App.Log("PersonDictionary Saving...");

          processedCodes.Add(firstItem.PersonalCode);
          uniqueSaveQueue.Enqueue(firstItem);

          while (saveItems.TryTake(out SaveItem? item)) {
            if (processedCodes.Contains(item.PersonalCode)) continue;
            processedCodes.Add(item.PersonalCode);
            uniqueSaveQueue.Enqueue(item);
          }

          WriteJsonZip(uniqueSaveQueue);
        } finally {
          App.Log("PersonDictionary Saved");
          savingEvent.Signal();
        }
      }
    }

    private void WriteJsonZip(Queue<SaveItem> uniqueSaveQueue) {
      // Can access person only in UI thread
      App.Run(() => {
        // Update jsonList
        while (uniqueSaveQueue.TryDequeue(out SaveItem? saveItem) && saveItem != null) {
          switch (saveItem.SaveType) {
            case SaveType.WRITE:
              if (TryGetValue(saveItem.PersonalCode, out Person? p)) {
                jsonList[saveItem.PersonalCode] = JsonSerializer.SerializeToElement(p, JSON_OPTIONS);
              }
              break;
            case SaveType.DELETE:
              jsonList.Remove(saveItem.PersonalCode);
              break;
          }
        };
      });

      try {
        using SafeFileStream fs = new(path, FileMode.Create, FileAccess.Write);
        using ZipArchive zip = new(fs, ZipArchiveMode.Create);
        ZipArchiveEntry entry = zip.CreateEntry(zipEntryName);
        using Stream entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, jsonList, JSON_OPTIONS); // TODO is it thread safe?
      } catch (IOException ex) {
        throw new IOException($"Failed saving to '{path}' ({ex.Message})", ex);
      }

    }

    #endregion

    #region IDictionary<string, Person>

    ICollection<string> IDictionary<string, Person>.Keys => map.Keys;
    ICollection<Person> IDictionary<string, Person>.Values => map.Values;

    public int Count => map.Count;

    bool ICollection<KeyValuePair<string, Person>>.IsReadOnly => false;

    public IEnumerable<string> Keys => map.Keys;

    public IEnumerable<Person> Values => map.Values;

    public Person this[string key] {
      get => map[key];
      set {
        string oldCode = value.PersonalCode;
        value.PersonalCode = key;
        UpdatePersonalCode(value, oldCode);
      }
    }

    void IDictionary<string, Person>.Add(string _, Person value) {
      Add(value);
    }

    public bool ContainsKey(string key) {
      return map.ContainsKey(key);
    }

    public bool Remove(string key) {
      Person? p = this.GetValueOrDefault(key);
      if (p != null) {
        Remove(p);
        return true;
      }
      return false;
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Person value) {
      return map.TryGetValue(key, out value);
    }

    void ICollection<KeyValuePair<string, Person>>.Add(KeyValuePair<string, Person> item) {
      Add(item.Value);
    }

    public bool Contains(KeyValuePair<string, Person> item) {
      return map.ContainsKey(item.Value.PersonalCode);
    }

    void ICollection<KeyValuePair<string, Person>>.CopyTo(KeyValuePair<string, Person>[] array, int arrayIndex) {
      ((ICollection<KeyValuePair<string, Person>>)map).CopyTo(array, arrayIndex);
    }

    public void Clear() {
      map.Clear();
      jsonList.Clear();
      Cleared?.Invoke();

      Delete();

      foreach (Column column in Config.Instance.Columns) {
        if (column is ComboBoxColumn comboBoxColumn && comboBoxColumn.Form != null) {
          comboBoxColumn.Form.ResetAutoFill();
        }
      }
    }

    bool ICollection<KeyValuePair<string, Person>>.Remove(KeyValuePair<string, Person> item) {
      return Remove(item.Value.PersonalCode);
    }

    public IEnumerator<KeyValuePair<string, Person>> GetEnumerator() {
      return map.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    #endregion

  }
}
