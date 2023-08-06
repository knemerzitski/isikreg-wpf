using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Controls;
using IsikReg.Extensions;
using IsikReg.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace IsikReg.Model {

  // TODO what if registration date is duplicate??? merge? consider registration to be the same
  public class Registration : IDictionary<string, object>, IReadOnlyDictionary<string, object>, IComparer<Registration>, IComparable<Registration> {

    public static string GetBindingPath(Column column) {
      switch (column.Group) {
        case ColumnGroup.PERSON:
          return $"Person.Properties[{column.Key.EscapedIndexer()}]";
        case ColumnGroup.REGISTRATION:
        default:
          return $"Properties[{column.Key.EscapedIndexer()}]";
      }
    }

    public bool Removed { get; private set; }

    public Person Person { get; }

    public RegistrationProperties Properties { get; }

    public bool Registered {
      get => RegisteredDate != null;
      set => RegisteredDate = value ? DateTime.Now : null;
    }

    public DateTime? RegisteredDate {
      get => Properties.GetDateTime(ColumnId.REGISTER_DATE);
      set {
        if (value != null) {
          Properties.Set(ColumnId.REGISTER_DATE, value);
        } else {
          Properties.Remove(ColumnId.REGISTER_DATE);
        }
      }
    }

    public Property<string?> RegistrationTypeProperty { get; } = new();
    public string RegistrationType {
      get => Properties.GetString(ColumnId.REGISTRATION_TYPE);
      set => Properties.Set(ColumnId.REGISTRATION_TYPE, value);
    }

    public Registration(Person person, IEnumerable<KeyValuePair<string, object>>? properties = null) {
      Properties = new(this);
      Properties.PropertyChanged += OnPropertyChanged;
      Person = person;

      if (properties != null) {
        foreach ((var key, var value) in properties.GetGroup(ColumnGroup.REGISTRATION)) {
          Properties.Add(key, value);
        }
      }
    }

    private void OnPropertyChanged(string key, object? oldValue, object? newValue, DictionaryAction action) {
      switch (Config.Instance.Columns.GetValueOrDefault(key)?.Id) {
        case ColumnId.REGISTRATION_TYPE:
          RegistrationTypeProperty.Value = newValue as string;
          break;
        case ColumnId.REGISTER_DATE:
          if (newValue == null && Person.Registrations.Count > 1) {
            Remove();
            return;
          }
          break;
      }
    }


    #region IDictionary<string, object>

    private bool TryGetDictionary(string key, [MaybeNullWhen(false)] out ObservableDictionary<string, object> dictionary, [MaybeNullWhen(false)] out string realKey) {
      if (Config.Instance.Columns.TryGetValue(key, out Column? column)) {
        realKey = column.Key;
        switch (column.Group) {
          case ColumnGroup.REGISTRATION:
            dictionary = Properties;
            return true;
          case ColumnGroup.PERSON:
            dictionary = Person.Properties;
            return true;
        }
      }
      dictionary = default;
      realKey = default;
      return false;
    }

    public IEnumerable<string> Keys => Properties.Keys.Concat(Person.Properties.Keys);

    public IEnumerable<object> Values => Properties.Values.Concat(Person.Properties.Values);

    public int Count => Properties.Count + Person.Properties.Count;

    ICollection<string> IDictionary<string, object>.Keys => Keys.ToArray();

    ICollection<object> IDictionary<string, object>.Values => Values.ToArray();

    public bool IsReadOnly => false;

    public object this[string key] {
      get {
        if (TryGetDictionary(key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
          return dictionary[realKey];
        }
        throw new KeyNotFoundException(key);
      }
      set {
        if (TryGetDictionary(key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
          dictionary[realKey] = value;
        } else {
          throw new KeyNotFoundException(key);
        }
      }
    }

    public void Add(string key, object value) {
      if (TryGetDictionary(key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        dictionary.Add(realKey, value);
      }
    }

    public bool ContainsKey(string key) {
      if (TryGetDictionary(key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        return dictionary.ContainsKey(realKey);
      }
      return false;
    }

    public bool Remove(string key) {
      if (TryGetDictionary(key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        return dictionary.Remove(realKey);
      }
      return false;
    }
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) {
      if (TryGetDictionary(key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        if (dictionary.TryGetValue(realKey, out value)) {
          return true;
        }
      }
      value = default;
      return false;
    }

    void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item) {
      if (TryGetDictionary(item.Key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        dictionary.Add(new(realKey, item.Value));
      }
    }

    public void Clear() {
      Properties.Clear();
    }

    bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item) {
      if (TryGetDictionary(item.Key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        return dictionary.Contains(new(realKey, item.Value));
      }
      return false;
    }

    void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) {
      ((ICollection<KeyValuePair<string, object>>)Properties).CopyTo(array, arrayIndex);
      ((ICollection<KeyValuePair<string, object>>)Person.Properties).CopyTo(array, arrayIndex + Properties.Count);
    }

    bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item) {
      if (TryGetDictionary(item.Key, out ObservableDictionary<string, object>? dictionary, out string? realKey)) {
        return ((ICollection<KeyValuePair<string, object>>)dictionary).Remove(new(realKey, item.Value));
      }
      return false;
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
      return new ConcatEnumerator<KeyValuePair<string, object>>(Properties.GetEnumerator(), Person.Properties.GetEnumerator());
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    #endregion

    public bool SetDefaultRegistrationType() {
      string? type = Config.Instance.GetDefaultRegistrationType();
      if (type != null) {
        RegistrationType = type;
        return true;
      }
      return false;
    }

    public bool ConfirmClearRegistration() {
      return App.ShowConfirmDialog("Oled kindel, et tahad " + RegistrationType.ToLower() + " registreerimist tühistada?", Person.GetDisplayInfo());
    }

    public void Reset() {
      RegisteredDate = null;
      SetDefaultRegistrationType();
    }

    public bool IsReset() {
      return !Registered && RegistrationType.Equals(Config.Instance.GetDefaultRegistrationType());
    }

    public bool IsOnlyRegistration() {
      return Person.Registrations.Count == 1;
    }

    public double? RemainingGracePeriodMillis() {
      DateTime? registrationDate = RegisteredDate;
      if (registrationDate == null) return null;

      TimeSpan graceDuration = TimeSpan.FromMilliseconds(Config.Instance.General.RegisterGracePeriod);

      return ((DateTime)registrationDate + graceDuration - DateTime.Now).TotalMilliseconds;
    }

    public void Merge(IReadOnlyDictionary<string, object> properties) {
      Properties.Merge(properties.GetGroup(ColumnGroup.REGISTRATION));
    }

    public bool UpdatePersonOrRegistrationShowForm() {
      List<string> labelList = new();
      IReadOnlyDictionary<string, object> initialValues = this;  
      if (Config.Instance.General.UpdatePerson) {
        labelList.Add("isikut");
      }
      labelList.Add("registreeringut");

      while (true) {
        RegistrationFormDialog regForm = new(
            "Muuda " + string.Join("/", labelList) + (RegisteredDate != null ? " " + RegisteredDate : "")
            , "Muuda", true);
        regForm.SetValues(initialValues);

        if (!Config.Instance.General.UpdatePerson) {
          foreach ((var column, var node) in regForm.GetFormNodes()) {
            if (column.Group == ColumnGroup.PERSON) {
              node.IsEnabled = false;
            }
          }
        }

        var newProps = regForm.ShowAndWait();
        if (newProps != null) {
          if (!Person.PersonalCode.Equals(newProps.GetPersonalCode())) {
            if (PersonDictionary.Instance.ContainsKey(newProps.GetPersonalCode())) {
              App.ShowWarningDialog($"Isikukoodiga '{newProps.GetPersonalCode()}' isik on juba olemas!", Person.GetDisplayInfo());
              newProps.Set(ColumnId.PERSONAL_CODE, Person.PersonalCode); // Revert
              initialValues = newProps;
              continue;
            }
          }

          this.SetRange(newProps);
          Properties.UpdateFormAutoFillIndex();
          Person.Properties.UpdateFormAutoFillIndex();
          return true;
        }
        return false;
      }
    }

    public int Compare(Registration? x, Registration? y) {
      if (x != null && y != null) {
        if (x.RegisteredDate != null && y.RegisteredDate != null) {
          DateTime xt = (DateTime)x.RegisteredDate;
          DateTime yt = (DateTime)y.RegisteredDate;
          return DateTime.Compare(xt, yt);
        } else if (x.RegisteredDate == null) {
          return -1;
        } else if (y.RegisteredDate == null) {
          return 1;
        }
      } else if (x == null) {
        return -1;
      } else if (y == null) {
        return 1;
      }
      return 0;
    }

    public int CompareTo(Registration? other) {
      return Compare(this, other);
    }

    public override string ToString() {
      return Properties.ToDisplayString();
    }
    public void Remove() {
      if (Removed) return;
      Removed = true;

      Registered = false; // Let all listeners do the counting
      Person.Registrations.Remove(this);
    }

  }
}
