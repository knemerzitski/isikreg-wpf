using IsikReg.Ui.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IsikReg.Configuration {
  public class VariableStatusMessages {

    public class Converter : JsonConverter<VariableStatusMessages> {
      public override VariableStatusMessages? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        switch (reader.TokenType) {
          case JsonTokenType.StartArray:
            List<string>? formats = (List<string>?)JsonSerializer.Deserialize(ref reader, typeof(List<string>), options);
            if (formats != null)
              return new VariableStatusMessages(formats);
            break;
          case JsonTokenType.String:
            string? s = reader.GetString();
            if (s != null)
              return new VariableStatusMessages(s);
            break;
        }
        return null;
      }

      public override void Write(Utf8JsonWriter writer, VariableStatusMessages variableStatusMessages, JsonSerializerOptions options) {
        if (variableStatusMessages.messages.Count == 0) {
          writer.WriteNullValue();
        } else if (variableStatusMessages.messages.Count == 1) {
          writer.WriteStringValue(variableStatusMessages.messages.ElementAt(0).GetFormat());
        } else {
          JsonSerializer.Serialize(writer, variableStatusMessages.messages.Select(v => v.GetFormat()), options);
        }
      }
    }

    private readonly List<VariableStatusMessage> messages;

    public VariableStatusMessages() {
      messages = new();
    }

    public VariableStatusMessages(params string[] formats) : this(formats.ToList()) {
    }

    public VariableStatusMessages(IEnumerable<string> formats) {
      messages = formats.Select(f => VariableStatusMessage.Parse(f)).ToList();
    }

    public List<VariableStatusMessage> GetMessages() {
      return messages;
    }

    public string ToString(IReadOnlyDictionary<string, string> variableMapper, string evnt = "") {
      VariableStatusMessage? msg = messages.Where(m => m.HasAllVariables(variableMapper)).FirstOrDefault();
      if (msg == null && messages.Count > 0)
        msg = messages.ElementAt(0);
      return msg != null ? msg.ToString(variableMapper, evnt) : "";
    }

  }
}
