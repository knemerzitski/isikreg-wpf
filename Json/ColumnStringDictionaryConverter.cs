using IsikReg.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using IsikReg.Configuration.Columns;
using IsikReg.Configuration;

namespace IsikReg.Json {
  public class ColumnStringDictionaryConverter<T> : JsonConverter<IDictionary<string, T>> {
    public override IDictionary<string, T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      reader.Read(); //StartObject

      Dictionary<string, T> dict = new();

      while (reader.TokenType == JsonTokenType.PropertyName) {
        string? key = reader.GetString();
        reader.Read(); // PropertyName
        if (key == null) {
          reader.Skip(); reader.Read(); // Skip null key
          continue;
        }
        Column? column = Config.Instance.Columns.GetValueOrDefault(key);
        if (column == null || !column.Serialize) {
          reader.Skip(); // Anything inside value
          reader.Read(); // End of inside value
          continue;
        }

        Type type = column.GetValueType();
        object? value = JsonSerializer.Deserialize(ref reader, type, options);
        if (value is T tValue) {
          dict[column.Key] = tValue;
        }

        reader.Read(); // EndProperty
      }

      //reader.Read(); // EndObject

      return dict;
    }

    public override void Write(Utf8JsonWriter writer, IDictionary<string, T> dict, JsonSerializerOptions options) {
      writer.WriteStartObject();
      foreach ((string key, T value) in dict) {
        if (value == null ||
          value is string str && string.IsNullOrWhiteSpace(str) ||
          value is bool b && !b) continue; // TODO don't save false?

        if (Config.Instance.Columns.TryGetValue(key, out Column? column) && column.Serialize) {
          writer.WritePropertyName(column.Key);
          JsonSerializer.Serialize(writer, value, options);
        }
      }
      writer.WriteEndObject();
    }

    public override bool CanConvert(Type typeToConvert) {
      return typeof(IDictionary<string, T>).IsAssignableFrom(typeToConvert);
    }
  }

}
