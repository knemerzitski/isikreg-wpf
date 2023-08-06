using IsikReg.Configuration.Columns;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static IsikReg.Configuration.Columns.OptionsColumn;

namespace IsikReg.Configuration {
  public partial class ColumnDictionary : IEnumerable<Column> {

    public class Converter : JsonConverter<ColumnDictionary> {
      public override ColumnDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonObject>?>(ref reader, options);
        if (dict == null) throw new JsonException("Failed to deserialize columns");

        List<Column> columns = new(dict.Count);
        foreach ((var label, var columnObject) in dict) {
          columnObject["Label"] = label;
          if(Enum.TryParse(columnObject["Id"]?.AsValue().ToString(), out ColumnId id) && id != ColumnId.NULL) {
            columnObject["Key"] = ToKey(id.ToString());
          } else {
            columnObject["Key"] = ToKey(label);
          }
          ColumnType colType = ColumnType.TEXT;
          if (columnObject.TryGetPropertyValue("Type", out JsonNode? value) && value != null) {
            string type = value.GetValue<string>();
            if (Enum.TryParse(type, out ColumnType result)) {
              colType = result;
            }
          }
          Column? column = (Column?)columnObject.Deserialize(colType.GetColumnType(), options);
          if (column == null) throw new JsonException($"Failed to deserialize column {label}");
          columns.Add(column);
        }

        return new ColumnDictionary(columns);
      }

      public override void Write(Utf8JsonWriter writer, ColumnDictionary dict, JsonSerializerOptions options) {
        // Remove label from column and uses it as key
        writer.WriteStartObject();
        foreach (Column column in dict) {
          writer.WritePropertyName(column.Label);
          JsonNode? node = JsonSerializer.SerializeToNode((object)column, options);
          if (node == null) throw new JsonException($"Failed to serialize column {column.Label}");
          JsonObject obj = node.AsObject();
          obj.Remove("Label");
          obj.Remove("Key");
          JsonSerializer.Serialize(writer, obj, options);
        }
        writer.WriteEndObject();
      }
    }

    private static readonly Column[] DEFAULT_COLUMNS = new Column[]{
      new CheckBoxColumn {
        Label = "Registreeritud",
        Id = ColumnId.REGISTERED,
        Group = ColumnGroup.REGISTRATION,
        Form = null,
        Table = new() {
          Editable = true,
        },
        Statistics = new() {
          Total = true,
          Percent = true,
        },
      },
      new RadioColumn {
        Label = "Registreerimise\ntüüp",
        Id = ColumnId.REGISTRATION_TYPE,
        Group = ColumnGroup.REGISTRATION,
        Required = true,
        Form = new() {
          Layout = Config.Orientation.HORIZONTAL,
        },
        Options = new Option[]{
          new(){ Label = "Sisse" },
          new(){ Label = "Välja" },
        },
      },
      new DateColumn {
        Label = "Registreerimise\naeg",
        Id = ColumnId.REGISTER_DATE,
        Group = ColumnGroup.REGISTRATION,
        Form = null,
        DateFormat = "dd.MM HH:mm",
      },
      new TextColumn {
        Label = "Isikukood",
        Id = ColumnId.PERSONAL_CODE,
        Group = ColumnGroup.PERSON,
        Required = true,
      },
      new TextColumn {
        Label = "Perekonnanimi",
        Id = ColumnId.LAST_NAME,
        Group = ColumnGroup.PERSON,
      },
      new TextColumn {
        Label = "Eesnimi",
        Id = ColumnId.FIRST_NAME,
        Group = ColumnGroup.PERSON,
      }
    };
    //      new Column(PERSON, SEX, Column.Type.RADIO, "Sugu", true, true, true),
    //      new Column(PERSON, CITIZENSHIP, Column.Type.TEXT, "Kodakondsus", true, false, true),
    //      new Column(PERSON, DATE_OF_BIRTH, Column.Type.DATE, "Sünniaeg", true, false, true),
    //      new Column(PERSON, PLACE_OF_BIRTH, Column.Type.TEXT, "Sünnikoht", true, false, true),
    //      new Column(PERSON, DOCUMENT_NR, Column.Type.TEXT, "Dokumendi number", true, false, true),
    //      new Column(PERSON, EXPIRY_DATE, Column.Type.DATE, "Kehtiv kuni", true, false, true),
    //      new Column(PERSON, DATE_OF_ISSUANCE, Column.Type.DATE, "Välja antud", true, false, true),
    //      new Column(PERSON, PLACE_OF_ISSUANCE, Column.Type.TEXT, "Välja andmise koht", true, false, true),
    //      new Column(PERSON, TYPE_OF_RESIDENCE_PERMIT, Column.Type.TEXT, "Elamisloa tüüp", true, false, true),
    //      new Column(PERSON, NOTES_LINE1, Column.Type.TEXT, "Märkused 1", true, false, true),
    //      new Column(PERSON, NOTES_LINE2, Column.Type.TEXT, "Märkused 2", true, false, true),
    //      new Column(PERSON, NOTES_LINE3, Column.Type.TEXT, "Märkused 3", true, false, true),
    //      new Column(PERSON, NOTES_LINE4, Column.Type.TEXT, "Märkused 4", true, false, true),
    //      new Column(PERSON, NOTES_LINE5, Column.Type.TEXT, "Märkused 5", true, false, true),

    [GeneratedRegex("\\s")]
    private static partial Regex WhitespaceRegex();
    private static readonly Regex Whitespace = WhitespaceRegex();
    public static string ToKey(string value) {
      return Whitespace.Replace(value, "").ToLower();
    }

    private readonly Dictionary<string, Column> columnsByKey = new();
    private readonly Dictionary<ColumnId, Column> columnsById = new();
    private readonly List<Column> columns = new();

    public ColumnDictionary() {
      Init(DEFAULT_COLUMNS);
    }

    public ColumnDictionary(IEnumerable<Column> columns) {
      Init(columns);
    }

    private void Init(IEnumerable<Column> columns) {
      foreach (Column column in columns) {
        Init(column);
      }

      // Don't save REGISTERED column as it can be derived from REGISTER_DATE column
      if (TryGetValue(ColumnId.REGISTERED, out Column? registeredColumn)) {
        registeredColumn.Serialize = false;
      }
    }

    private void Init(Column column) {
      column.Init();
      columns.Add(column);
      columnsByKey[ToKey(column.Label)] = column;
      columnsByKey[column.Key] = column;
      if (column.Id != ColumnId.NULL) {
        columnsById[column.Id] = column;
      }
    }

    public void ApplyConfig(Config config) {
      // Add hidden expired date column if checking expire date is needed
      if (config.SmartCard.RegisterExpiredCards != Config.Rule.ALLOW && !Contains(ColumnId.EXPIRY_DATE)) {
        Init(new Column() {
          Id = ColumnId.EXPIRY_DATE,
          Group = ColumnGroup.PERSON,
          Table = null,
          Serialize = false,
        });
      }
    }

    public IEnumerable<Column> Get(ColumnGroup group) {
      return Values.Where(e => e.Group == group);
    }

    public Column this[ColumnId id] {
      get {
        if (TryGetValue(id, out Column? column)) {
          return column;
        }
        throw new KeyNotFoundException(id.ToString());
      }
    }

    public Column this[string key] {
      get {
        if (TryGetValue(key, out Column? column)) {
          return column;
        }
        throw new KeyNotFoundException(key);
      }
    }

    public Column? GetValueOrDefault(ColumnId id, Column? column = default) {
      return TryGetValue(id, out Column? result) ? result : column;
    }

    public Column? GetValueOrDefault(string key, Column? column = default) {
      return TryGetValue(key, out Column? result) ? result : column;
    }

    public IEnumerable<string> Keys => columnsByKey.Keys;

    public IEnumerable<ColumnId> Ids => columnsById.Keys;

    public IEnumerable<Column> Values => columns;

    public int Count => columns.Count;

    public bool Contains(string key) {
      return columnsByKey.ContainsKey(key);
    }

    public bool Contains(ColumnId id) {
      return columnsById.ContainsKey(id);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Column column) {
      return columnsByKey.TryGetValue(key, out column);
    }

    public bool TryGetValue(ColumnId id, [MaybeNullWhen(false)] out Column column) {
      return columnsById.TryGetValue(id, out column);
    }

    public IEnumerator<Column> GetEnumerator() {
      return columns.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }


  }
}
