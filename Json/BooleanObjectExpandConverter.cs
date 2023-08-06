using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IsikReg.Json {
  // TODO fix issue with DateForm
  public class BooleanObjectExpandConverter<T> : DefaultConverterFactory<T> {
    public override bool HandleNull => true;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions modifiedOptions) {
      switch (reader.TokenType) {
        case JsonTokenType.StartObject:
          return base.Read(ref reader, typeToConvert, modifiedOptions);
        case JsonTokenType.True:
        case JsonTokenType.False:
          bool createObject = reader.GetBoolean();
          if (createObject) {
            return (T?)JsonSerializer.Deserialize(new JsonObject(), typeToConvert, modifiedOptions);
          }
          break;
      }
      return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions modifiedOptions) {
      if (value != null) {
        base.Write(writer, value, modifiedOptions);
      } else {
        writer.WriteBooleanValue(false);
      }
    }

    public override bool CanConvert(Type typeToConvert) {
      return typeof(T).IsAssignableFrom(typeToConvert);
    }
  }
}
