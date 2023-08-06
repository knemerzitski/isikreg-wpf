using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IsikReg.Json {
  public class JsonStringDefaultEnumConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? s = reader.GetString();
                if (Enum.TryParse(typeToConvert, s, true, out object? result))
                {
                    return result;
                }
            }
            Array values = Enum.GetValues(typeToConvert);
            return values.Length > 0 ? values.GetValue(0) : null;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }
    }

}
