using IsikReg.Json;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IsikReg.Configuration.Columns {

  // TODO create general required field instead of just form, that means it cant be empty!!!!!

  public enum ColumnType {
    NULL,
    TEXT,
    CHECKBOX,
    DATE,
    COMBOBOX,
    RADIO
  }

  public static class ColumnTypeExtension {
    public static Type GetColumnType(this ColumnType type) {
      return type switch {
        ColumnType.TEXT => typeof(TextColumn),
        ColumnType.CHECKBOX => typeof(CheckBoxColumn),
        ColumnType.DATE => typeof(DateColumn),
        ColumnType.COMBOBOX => typeof(ComboBoxColumn),
        ColumnType.RADIO => typeof(RadioColumn),
        _ => throw new ArgumentOutOfRangeException(type.ToString()),
      };
    }

  }

  public enum ColumnGroup {
    PERSON, REGISTRATION
  }

  public enum ColumnId {
    NULL, // Custom
    REGISTERED, REGISTER_DATE, REGISTRATION_TYPE, // Per registration

    LAST_NAME, FIRST_NAME, SEX, CITIZENSHIP, // Per person, can be read from id card
    DATE_OF_BIRTH, PLACE_OF_BIRTH,
    PERSONAL_CODE,
    DOCUMENT_NR, EXPIRY_DATE,
    DATE_OF_ISSUANCE, PLACE_OF_ISSUANCE,
    TYPE_OF_RESIDENCE_PERMIT,
    NOTES_LINE1, NOTES_LINE2, NOTES_LINE3, NOTES_LINE4, NOTES_LINE5,
  }

  public class ColumnTable {
    public bool Editable { get; init; } = false;

    [JsonIgnore]
    public bool IsReadOnly => !Editable;
  }

  public class ColumnStatistics {
    public bool Total { get; init; } = false;
    public bool Percent { get; init; } = false;

  }

  public class ColumnMerge {

    public enum RuleType {
      OVERWRITE_ON_EMPTY, COMBINE, NEWER, OLDER
    }

    public RuleType Rule { get; init; } = RuleType.OVERWRITE_ON_EMPTY;
    public string Separator { get; init; } = "; ";

    public class Converter : DefaultConverterFactory<ColumnMerge> {

      public override ColumnMerge Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions modifiedOptions) {
        if (reader.TokenType == JsonTokenType.StartObject) {
          return base.Read(ref reader, typeToConvert, modifiedOptions)!;
        } else {
          string? rule = reader.GetString();
          if (Enum.TryParse(rule, true, out RuleType ruleEnum)) {
            return new ColumnMerge {
              Rule = ruleEnum,
            };
          } else {
            return new ColumnMerge {
              Rule = RuleType.OVERWRITE_ON_EMPTY,
            };
          }

        }
      }

      public override void Write(Utf8JsonWriter writer, ColumnMerge merge, JsonSerializerOptions modifiedOptions) {
        if (merge.Rule == RuleType.OVERWRITE_ON_EMPTY) {
          writer.WriteStringValue(Enum.GetName(merge.Rule));
        } else {
          base.Write(writer, merge, modifiedOptions);
        }
      }
    }

  }

  public class Column {

    [JsonIgnore]
    public bool Serialize = true;

    public string Key { get; init; } = string.Empty;

    public ColumnId Id { get; init; } = ColumnId.NULL;
    public ColumnGroup Group { get; init; } = ColumnGroup.PERSON;

    public virtual ColumnType Type { get; } = ColumnType.NULL;

    public string Label { get; init; } = string.Empty;

    public bool Required { get; init; } = false;

    // Table
    public ColumnTable? Table { get; init; } = new();

    public ColumnMerge Merge { get; init; } = new();

    public ColumnStatistics? Statistics { get; init; } = null;

    public Column() { }

    public virtual void Init() {
    }

    public virtual Type GetValueType() {
      return typeof(object);
    }
    public virtual bool HasForm() {
      return false;
    }

    public override string? ToString() {
      string val = Key;
      return !string.IsNullOrWhiteSpace(val) ? $"Column: {val}" : base.ToString();
    }


  }

}
