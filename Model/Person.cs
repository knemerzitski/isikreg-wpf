using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Controls;
using IsikReg.Extensions;
using IsikReg.Properties;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IsikReg.Model {

  public class Person {

    public class Converter : JsonConverter<Person> {

      public Converter() {
      }

      public override Person? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartObject) {
          throw new JsonException($"Unexpected token {reader.TokenType}. Person must be an object");
        }

        reader.Read(); //StartObject

        Dictionary<string,object>? properties = null;
        List<Dictionary<string,object>>? registrations = null;

        while (reader.TokenType == JsonTokenType.PropertyName) {
          string? key = reader.GetString();
          reader.Read(); // PropertyName
          switch (key) {
            case "properties":
              properties = JsonSerializer.Deserialize<Dictionary<string, object>?>(ref reader, options);
              break;
            case "registrations":
              registrations = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(ref reader, options);
              break;
          }
          reader.Read(); // End Property
        }

        if (properties == null) {
          throw new JsonException("Failed to deserialize Person. Key 'properties' is missing");
        }

        //reader.Read(); // EndObject

        return new Person(properties, registrations);
      }

      public override void Write(Utf8JsonWriter writer, Person person, JsonSerializerOptions options) {
        writer.WriteStartObject();

        writer.WritePropertyName("properties");
        JsonSerializer.Serialize(writer, person.Properties, options);

        if (person.Registrations.Count > 0) {
          writer.WritePropertyName("registrations");
          JsonSerializer.Serialize(writer, person.Registrations.Select(r => r.Properties), options);
        }

        writer.WriteEndObject();
      }
    }

    private static readonly string REGISTER_DATE_KEY = Config.Instance.Columns[ColumnId.REGISTER_DATE].Key;

    public bool Removed { get; private set; }

    public PersonProperties Properties { get; }
    public ObservableCollection<Registration> Registrations { get; } = new();

    public string PersonalCode {
      get => Properties.GetString(ColumnId.PERSONAL_CODE);
      set => Properties.Set(ColumnId.PERSONAL_CODE, value);
    }

    public string LastName {
      get => Properties.GetString(ColumnId.LAST_NAME);
      set => Properties.Set(ColumnId.LAST_NAME, value);
    }

    public string FirstName {
      get => Properties.GetString(ColumnId.FIRST_NAME);
      set => Properties.Set(ColumnId.FIRST_NAME, value);
    }

    public Property<Registration?> LatestRegisteredRegistrationProperty { get;  } = new();
    public Registration? LatestRegisteredRegistration {
      get => LatestRegisteredRegistrationProperty.Value;
      private set {
        LatestRegisteredRegistrationProperty.Value = value;
        if (value == null) {
          RegisteredTypeProperty.Unbind();
          RegisteredTypeProperty.Value = null;
        } else {
          RegisteredTypeProperty.Bind(value.RegistrationTypeProperty);
        }
      }
    }

    public Property<string?> RegisteredTypeProperty { get; } = new();
    public string? RegisteredType => LatestRegisteredRegistration?.RegistrationType;

    public Person(IEnumerable<KeyValuePair<string, object>>? properties = null, IEnumerable<IEnumerable<KeyValuePair<string, object>>>? registrationProperties = null) {
      Properties = new(this);

      Registrations.CollectionChanged += OnRegistrationsChanged;

      if (properties != null) {
        if (properties.AnyGroup(ColumnGroup.REGISTRATION)) {
          NewRegistration(properties);
        }
      }

      if (registrationProperties != null) {
        foreach (var props in registrationProperties) {
          NewRegistration(props);
        }
      }

      if (Registrations.Count == 0) {
        Registration r = NewRegistration();
        r.SetDefaultRegistrationType();
      }

      if (properties != null) {
        foreach ((var key, var value) in properties.GetGroup(ColumnGroup.PERSON)) {
            Properties.Add(key, value);
        }
      }
    }

    private void OnRegistrationsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      if (e.NewItems != null) {
        foreach (Registration newR in e.NewItems) {
          void latestRegistrationListener(string key, object? oldValue, object? newValue, DictionaryAction action) {
            if (Config.Instance.Columns.GetValueOrDefault(key)?.Id != ColumnId.REGISTER_DATE) return;
            Registration? curR = LatestRegisteredRegistration;
            Registration? nextR = curR == newR ? Registrations.Max() : newR.CompareTo(curR) == 1 ? newR : curR;
            if(nextR?.Registered == true) {
              LatestRegisteredRegistration = nextR;
            } else {
              LatestRegisteredRegistration = null;
            }
          }

          if (newR.RegisteredDate != null) {
            latestRegistrationListener(REGISTER_DATE_KEY, null, newR.RegisteredDate, DictionaryAction.CHANGED);
          }
          newR.Properties.PropertyChanged += latestRegistrationListener;
        }
      }
    }

    public void RemoveRegistrations() {
      Registrations.ToList().ForEach(r => r.Remove());
    }

    public void Merge(Person newPerson) {
      Properties.Merge(newPerson.Properties);

      foreach (Registration r in newPerson.Registrations) {
        MergeRegistration(r);
      }

      newPerson.Remove();
    }

    public void Merge(IReadOnlyDictionary<string, object> properties, IEnumerable<IReadOnlyDictionary<string, object>>? registrationProperties = null) {
      Properties.Merge(properties.GetGroup(ColumnGroup.PERSON));

      if (properties.AnyGroup(ColumnGroup.REGISTRATION)) {
        MergeRegistration(properties);
      }

      if (registrationProperties != null) {
        foreach (var r in registrationProperties) {
          MergeRegistration(r);
        }
      }
    }

    private Registration MergeRegistration(Registration registration) {
      Registration? sameReg = Registrations.Where(r => r.CompareTo(registration) == 0).FirstOrDefault();
      if (sameReg != null) {
        sameReg.Properties.Merge(registration.Properties);
        return sameReg;
      } else {
        return NewRegistration(registration.Properties);
      }
    }

    private Registration MergeRegistration(IReadOnlyDictionary<string, object> properties) {
      Registration? sameReg = Registrations
        .Where(r => {
          DateTime? d1 = r.RegisteredDate;
          DateTime? d2 = properties.GetRegisteredDate();
          if (d1 == null || d2 == null) return d1 == d2;
          return DateTime.Compare((DateTime)d1, (DateTime)d2) == 0;
        }).FirstOrDefault();

      if (sameReg != null) {
        sameReg.Merge(properties);
        return sameReg;
      } else {
        return NewRegistration(properties);
      }
    }

    /**
     * Person is added or updated in the list.
     * Properties might have changed and should be checked.
     */
    public void UpdateAutoFillIndex() {
      Properties.UpdateFormAutoFillIndex();
      foreach (Registration r in Registrations) {
        r.Properties.UpdateFormAutoFillIndex();
      }
    }

    public bool CheckRegistrationGracePeriod(string? desiredRegistrationType = null) {
      return CheckRegistrationGracePeriod(out _, desiredRegistrationType);
    }

    public bool CheckRegistrationGracePeriod(out bool dialogConfirmed, string? desiredRegistrationType = null) {
      dialogConfirmed = false;
      if (Config.Instance.General.RegisterDuringGracePeriod == Config.Rule.ALLOW) {
        return true;
      }
      Registration? latestRegisteredRegistration = LatestRegisteredRegistration;
      if (latestRegisteredRegistration == null)
        return true;
      DateTime? regDateNullable = latestRegisteredRegistration.RegisteredDate;
      if (regDateNullable == null)
        return true;
      DateTime regDate = (DateTime)regDateNullable;

      // TODO check if correctly implemented
      DateTime now = DateTime.Now; // TODO or utc?
      TimeSpan graceDur = TimeSpan.FromMilliseconds(Config.Instance.General.RegisterGracePeriod);
      string regText = desiredRegistrationType != null ? "Kas registreerin " + desiredRegistrationType.ToLower() + "?" : "Kas jätkan registreerimisega?";
      DateTime afterRegDate = now - graceDur;
      if (afterRegDate > regDate) {
        return true; // After grace period
      } else if (Config.Instance.General.RegisterDuringGracePeriod == Config.Rule.CONFIRM) {
        dialogConfirmed = App.ShowConfirmDialog(
            "On " + regDate.UntilText1(now) + " tagasi " + latestRegisteredRegistration.RegistrationType.ToLower() +
                " registreeritud.\n" + regText,
            GetDisplayInfo());
        return dialogConfirmed;
      } else if (Config.Instance.General.RegisterDuringGracePeriod == Config.Rule.DENY) {
        App.ShowWarningDialog(
            "On " + latestRegisteredRegistration.RegistrationType.ToLower() +
                " registreeritud!\nRegistreerida saab " + afterRegDate.UntilText2(regDate) + " pärast.",
            GetDisplayInfo());
        return false;
      }
      return false;
    }

    public bool CheckRegistrationSameTypeDeny(string desiredType) {
      if (Config.Instance.General.RegisterSameTypeInRow == Config.Rule.ALLOW) {
        return false;
      }

      Registration? latestReg = LatestRegisteredRegistration;
      if (latestReg == null)
        return false;

      string latestType = latestReg.RegistrationType;

      if (!latestType.Equals(desiredType))
        return false;

      return Config.Instance.General.RegisterSameTypeInRow == Config.Rule.DENY;
    }

    public bool CheckRegistrationSameTypeInRow(string desiredType, bool autoYesConfirm = false) {
      if (Config.Instance.General.RegisterSameTypeInRow == Config.Rule.ALLOW) {
        return true;
      }

      Registration? latestReg = LatestRegisteredRegistration;
      if (latestReg == null)
        return true;

      string latestType = latestReg.RegistrationType;

      if (!latestType.Equals(desiredType))
        return true;

      if (Config.Instance.General.RegisterSameTypeInRow == Config.Rule.CONFIRM) {
        return autoYesConfirm || App.ShowConfirmDialog(
            "On juba " + latestType.ToLower() + " registreeritud!\nKas registreerin veelkord " + desiredType.ToLower() + "?",
            GetDisplayInfo());
      } else if (Config.Instance.General.RegisterSameTypeInRow == Config.Rule.DENY) {
        App.ShowWarningDialog(
            "On juba " + latestType.ToLower() + " registreeritud!\nJärjest topelt sama registreerimine ei ole lubatud.",
            GetDisplayInfo());
        return false;
      }

      return false;
    }

    public bool CheckRegistrationAllowed(string desiredType) {
      bool dialogConfirmed = false;
      // (a | b) & c
      if (CheckRegistrationSameTypeDeny(desiredType) || CheckRegistrationGracePeriod(out dialogConfirmed, desiredType)) {
        return CheckRegistrationSameTypeInRow(desiredType, dialogConfirmed);
      }
      return false;
    }

    public string GetDisplayInfo() {
      return (RegisteredType != null ? RegisteredType + " " : "") + Properties.GetPersonDisplayInfo();
    }

    private void ShowAlreadyRegisteredWarning(string registrationType) {
      App.ShowWarningDialog("On juba " + registrationType.ToLower() + " registreeritud!", GetDisplayInfo());
    }

    public bool HasEmptyRegistration() {
      return Registrations.Any(r => !r.Registered);
    }

    public bool HasEmptyRegistration(string registrationType) {
      return Registrations.Any(r => r.RegistrationType.Equals(registrationType) && !r.Registered);
    }

    public string? GetNextRegistrationType() {
      List<string> options = Config.Instance.GetRegistrationTypes();

      Registration? latestRegisteredRegistration = LatestRegisteredRegistration;
      if (latestRegisteredRegistration != null) {
        string curType = latestRegisteredRegistration.RegistrationType;
        int index = options.IndexOf(curType);
        if (index != -1) {
          return options.ElementAt((index + 1) % options.Count);
        } else if (options.Count > 0) {
          return options.ElementAt(0);
        }
      } else {
        if (options.Count > 0) {
          return options.ElementAt(0);
        }
      }
      return null;
    }

    public Registration NewRegistration(IEnumerable<KeyValuePair<string, object>>? properties = null) {
      Registration reg = new(this, properties);
      Registrations.Add(reg);
      return reg;
    }

    public Registration NewNextRegistration() {
      return NewTypeRegistration(GetNextRegistrationType());
    }

    public Registration NewTypeRegistration(string? desiredType = null) {
      // Use next registration type
      if (desiredType != null) {
        Registration? regWithType = Registrations.Where(r => r.RegisteredDate == null && r.RegistrationType.Equals(desiredType)).FirstOrDefault();
        if (regWithType != null)
          return regWithType;

        Registration? regRandomType = Registrations.Where(r => r.RegisteredDate == null).FirstOrDefault();
        if (regRandomType != null) {
          regRandomType.RegistrationType = desiredType;
          return regRandomType;
        }

        Registration newReg = NewRegistration();
        newReg.RegistrationType = desiredType;
        return newReg;
      }

      Registration? emptyReg = Registrations.Where(r => r.RegisteredDate == null).FirstOrDefault();
      if (emptyReg != null)
        return emptyReg;

      Registration reg = NewRegistration();
      reg.SetDefaultRegistrationType();
      return reg;
    }

    public void CleanUpRegistrations() {
      foreach (Registration r in Registrations.Where(r => r.RegisteredDate == null).ToList()) {
        Registrations.Remove(r);
      }
      if (Registrations.Count == 0) {
        Registration r = NewRegistration();
        r.SetDefaultRegistrationType();
      }
    }

    public Registration? GetLatestRegistration(Registration skipThis) {
      if (Registrations.Count == 0)
        return null;
      for (int i = Registrations.Count - 1; i >= 0; i--) {
        Registration r = Registrations.ElementAt(i);
        if (r != skipThis)
          return r;
      }
      return null;
    }

    public Registration GetLatestRegistration() {
      if (Registrations.Count == 0)
        return NewNextRegistration();
      return Registrations.ElementAt(Registrations.Count - 1);
    }

    public void SetEmptyStringsToNull() {
      Properties.RemoveEmptyStrings();
      foreach (Registration r in Registrations) {
        r.Properties.RemoveEmptyStrings();
      }
    }

    //public bool Same(Person person) {
    //  if (!Properties.Same(person.Properties)) return false;

    //  List<Registration> remaining = new(person.Registrations);
    //  foreach (Registration r in Registrations) {
    //    bool found = false;
    //    for (int i = remaining.Count - 1; i >= 0; i--) {
    //      Registration r2 = remaining.ElementAt(i);
    //      if (r.Properties.Same(r2.Properties)) {
    //        remaining.RemoveAt(i);
    //        found = true;
    //        break;
    //      }
    //    }
    //    if (!found) return false;
    //  }
    //  return true;
    //}


    public Registration? InsertRegistrationConfirm(string registrationType) {
      if (!CheckRegistrationAllowed(registrationType)) return null;
      Registration r = NewTypeRegistration(registrationType);
      r.RegisteredDate = DateTime.Now;
      return r;
    }

    public Registration? InsertRegistrationShowForm(IReadOnlyDictionary<string, object> formValues) {
      if (!CheckRegistrationGracePeriod(out bool gracePeriodDialogConfirmed))
        return null;

      string? currentType = null;
      Registration? latestReg = LatestRegisteredRegistration;
      if (latestReg != null) {
        currentType = latestReg.RegistrationType;
      }

      RegistrationFormDialog regForm = new("Uut tüüpi registreerimine" +
        (currentType != null ? " (Hetkel " + currentType.ToLower() + " registreeritud)" : ""), "Registreeri", true);
      regForm.SetValues(formValues);

      string? type = GetNextRegistrationType();
      if (type != null)
        regForm.SetValue(ColumnId.REGISTRATION_TYPE, type);

      foreach ((var column, var node) in regForm.GetFormNodes()) {
        if (column.Group != ColumnGroup.REGISTRATION) {
          node.IsEnabled = false;

          // Enable if property is null or empty string
          object? value = formValues.GetValueOrDefault(column);
          if (value == null || (value is string str && string.IsNullOrWhiteSpace(str))) {
            node.IsEnabled = true;
          }
        }
      }

      Dictionary<string, object>? newProps = regForm.ShowAndWait();
      if (newProps == null) return null;

      string desiredType = newProps.GetRegistrationType();

      // check type of registration
      if (!CheckRegistrationSameTypeInRow(desiredType, gracePeriodDialogConfirmed))
        return null;

      // TODO use this in reference, compare to insert new registration????
      Registration r = NewTypeRegistration(desiredType);
      r.SetRange(newProps);
      r.Properties.UpdateFormAutoFillIndex();
      Properties.UpdateFormAutoFillIndex();
      r.RegisteredDate = DateTime.Now;
      return r;
    }

    public override string ToString() {
      return Properties.ToDisplayString() + " [" + string.Join(", ", Registrations.Select(r => "(" + r.ToString() + ")").ToList()) + "]";
    }

    public void Remove() {
      if (Removed) return;
      Removed = true;

      RemoveRegistrations();
    }

  }
}
