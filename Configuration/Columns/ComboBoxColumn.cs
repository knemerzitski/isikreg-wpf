using IsikReg.Extensions;
using IsikReg.Json;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IsikReg.Configuration.Columns {

  public class ComboBoxForm {

    public bool Editable { get; init; } = false;

    [JsonPropertyName("Default")]
    public string Initial { get; init; } = string.Empty;

    public StringOrBoolean? Autofill { get; init; } = null;

    public ObservableCollection<string> autofillValues = new();
    public Regex? autofillPattern = null;
    public int autofillIndex = 1;
    public int autoFillSelected = 0;
    public string? autoFillPrevious = null;
    public bool autoFillUpdateUsePrevious = true;

    public void ResetAutoFill() {
      if (Autofill == null)
        return;

      autofillValues.Clear();
      autofillIndex = 1;
      autoFillSelected = 0;
      autoFillPrevious = null;
    }

    public bool IsSimpleAutofill() {
      return Autofill != null && Autofill.IsBoolean() && Autofill.GetBoolean();
    }

  }

  public class ComboBoxColumn : OptionsColumn {


    public override ColumnType Type { get; } = ColumnType.COMBOBOX;

    public ComboBoxForm? Form { get; init; } = new();

    public ComboBoxColumn() {
    }

    public override void Init() {
      base.Init();

      if (Form == null) return;
      if (Form.Autofill != null) {
        if (Form.Autofill.IsString()) {
          Form.autofillPattern = Form.Autofill.GetString()?.FormatToPattern("{0:.+?}", "(\\d+)");
        }
      }

      if (Form != null && (Form.autofillPattern != null || Form.IsSimpleAutofill())) {
        foreach (string value in Form.autofillValues) {
          AllOptions.Add(value);
        }
        Form.autofillValues.CollectionChanged += (s, e) => {
          if (e.NewItems != null) {
            foreach (string value in e.NewItems) {
              AllOptions.Add(value);
            }
          }
          if (e.OldItems != null) {
            foreach (string value in e.OldItems) {
              AllOptions.Remove(value);
            }
          }
        };
      }
    }

    public override bool HasForm() {
      return Form != null;
    }

  }
}
