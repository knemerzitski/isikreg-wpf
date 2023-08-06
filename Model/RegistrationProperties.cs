using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using System;
using System.Collections.Generic;

namespace IsikReg.Model {
  public class RegistrationProperties : ObservableDictionary<object> {

    private readonly Registration registration;

    private static readonly string REGISTER_DATE_KEY = Config.Instance.Columns[ColumnId.REGISTER_DATE].Key;

    public override object? this[string key] {
      get {
        if (Config.Instance.Columns.TryGetValue(key, out Column? column)) {
          switch (column.Id) {
            case ColumnId.REGISTERED:
              return this.GetValueOrDefault(REGISTER_DATE_KEY) is DateTime;
          }
        }
        return base[key];
      }
      set {
        if (Config.Instance.Columns.TryGetValue(key, out Column? column)) {
          if (column.Required && 
              (value == null || 
              value is string s && string.IsNullOrWhiteSpace(s) ||
              value is bool b && !b)) {
            return;
          }
          switch (column.Id) {
            case ColumnId.REGISTERED:
              if (value is bool register) {
                if (register) {
                  if (registration.Person.CheckRegistrationAllowed(registration.RegistrationType)) {
                    base[REGISTER_DATE_KEY] = DateTime.Now;
                    PersonDictionary.Instance.Save(registration.Person);
                  }
                } else if (registration.ConfirmClearRegistration()) {
                  Remove(REGISTER_DATE_KEY);
                  PersonDictionary.Instance.Save(registration.Person);
                }
              }
              return;
          }
        }
        base[key] = value;
        PersonDictionary.Instance.Save(registration.Person);
      }
    }

    public RegistrationProperties(Registration registration) {
      this.registration = registration;
    }

    public RegistrationProperties(Registration registration, IEnumerable<KeyValuePair<string, object>> properties) : base(properties) {
      this.registration = registration;
    }
  }
}
