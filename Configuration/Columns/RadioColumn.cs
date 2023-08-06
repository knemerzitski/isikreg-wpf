using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static IsikReg.Configuration.Columns.Column;
using static IsikReg.Configuration.Columns.OptionsColumn;
using static IsikReg.Configuration.Columns.RadioColumn;

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
