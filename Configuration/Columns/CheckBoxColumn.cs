using System;
using System.Text.Json.Serialization;

namespace IsikReg.Configuration.Columns {

  public class CheckBoxForm {

    [JsonPropertyName("Default")]
    public bool Initial { get; init; } = false;
  }

  public class CheckBoxColumn : Column {

    public override ColumnType Type { get => ColumnType.CHECKBOX; }

    public CheckBoxForm? Form { get; init; } = new();

    public CheckBoxColumn() { }

    public override Type GetValueType() {
      return typeof(bool);
    }

    public override bool HasForm() {
      return Form != null;
    }


  }
}
