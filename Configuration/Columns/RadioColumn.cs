using System.Text.Json.Serialization;

namespace IsikReg.Configuration.Columns {

  public class RadioForm {

    [JsonPropertyName("Default")]
    public string Initial { get; init; } = string.Empty;

    public Config.Orientation Layout { get; init; } = Config.Orientation.VERTICAL;

  }
  public class RadioColumn : OptionsColumn {

    public override ColumnType Type { get; } = ColumnType.RADIO;

    public RadioForm? Form { get; init; } = new();

    public RadioColumn() {
    }

    public override bool HasForm() {
      return Form != null; ;
    }

  }
}
