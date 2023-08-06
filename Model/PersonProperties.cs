using IsikReg.Collections;
using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.Model {
  public class PersonProperties : ObservableDictionary<object> {

    public override object? this[string key] {
      get => base[key];
      set {
        if (Config.Instance.Columns.TryGetValue(key, out Column? column)) {
          if (column.Required &&
               (value == null ||
               value is string s && string.IsNullOrWhiteSpace(s) ||
               value is bool b && !b)) {
            return;
          }
          switch (column.Id) {
            case ColumnId.PERSONAL_CODE:
              if (value is string code && PersonDictionary.Instance.ContainsKey(code)) {
                return;
              }
              break;  
          }
        }
        base[key] = value;
        PersonDictionary.Instance.Save(person);
      }
    }

    private readonly Person person;

    public PersonProperties(Person person) {
      this.person = person;
    }

    public PersonProperties(Person person, IEnumerable<KeyValuePair<string, object>> properties) : base(properties) {
      this.person = person;
    }
  }
}
