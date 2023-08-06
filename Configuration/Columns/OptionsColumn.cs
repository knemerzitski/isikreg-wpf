using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using IsikReg.Json;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace IsikReg.Configuration.Columns {
  public class OptionsColumn : Column {

    public class Option {

      public class JsonConverter : DefaultConverterFactory<Option> {

        public override Option? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions modifiedOptions) {
          if (reader.TokenType == JsonTokenType.StartObject) {
            return base.Read(ref reader, typeToConvert, modifiedOptions);
          } else {
            string label = reader.GetString() ?? String.Empty;
            Option o = new() {
              Label = label
            };
            return o;
          }
        }

        public override void Write(Utf8JsonWriter writer, Option option, JsonSerializerOptions modifiedOptions) {
          if (option.Id != null) {
            base.Write(writer, option, modifiedOptions);
          } else {
            writer.WriteStringValue(option.Label);
          }
        }

      }

      public string? Id { get; init; } = null;
      public string Label { get; init; } = string.Empty;

      public Option() {
      }

      public override string ToString() {
        return Label;
      }

    }

    public Option[] Options { get; init; } = Array.Empty<Option>();

    [JsonIgnore]
    public ObservableCollection<string> AllOptions { get; } = new();

    public OptionsColumn() {
    }

    public override void Init() {
      base.Init();

      foreach(string o in GetOptionValues()){
        AllOptions.Add(o);
      }
    }

    public List<string> GetOptionValues() {
      return Options.Where(l => !string.IsNullOrWhiteSpace(l.Label)).Select(l => l.Label).ToList();
    }

    public override Type GetValueType() {
      return typeof(string);
    }

  }
}
