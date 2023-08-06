using System;
using System.Text.Json.Serialization;

namespace IsikReg.Configuration.Columns {

  public class TextForm {

    public bool Editable { get; init; } = true;

    [JsonPropertyName("Default")]
    public string Initial { get; init; } = string.Empty;

  }
  public class TextColumn : Column {

    public override ColumnType Type { get; } = ColumnType.TEXT;

    public TextForm? Form { get; init; } = new();

    public TextColumn() {
    }

    public override Type GetValueType() {
      return typeof(string);
    }

    public override bool HasForm() {
      return Form != null; ;
    }
  }
}
