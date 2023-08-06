using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using IsikReg.Configuration.Columns;
using IsikReg.Json;
using System.ComponentModel;
using Microsoft.VisualBasic;
using System.Text.Json.Serialization.Metadata;
using System.Collections;
using IsikReg.Ui;
using IsikReg.Extensions;
using System.Collections.ObjectModel;
using static IsikReg.Configuration.Columns.OptionsColumn;
using System.Text.RegularExpressions;
using IsikReg.Model;

namespace IsikReg.Configuration
{
    public class Config {

    private static readonly Lazy<Config> lazyInstance = new(() => {
      string path = "./settings.json";
      Config config = ReadFromJson(path);

      ConfigValidator.Validate(config);

      config.SaveNew(path);

      config.Columns.ApplyConfig(config);

      return config;
    });


    public static Config Instance { get => lazyInstance.Value; }


    private readonly static JsonSerializerOptions JSON_OPTIONS = new() {
      WriteIndented = true,
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Don't escape äöüö
      PropertyNameCaseInsensitive = true,
      TypeInfoResolver = new DefaultJsonTypeInfoResolver {
        Modifiers = {
          TypeInfoResolverModifiers.SkipEmptyCollections,
          TypeInfoResolverModifiers.SkipNullOrWhitespaceStrings,
          TypeInfoResolverModifiers.SkipNullProperties
        }
      },
      Converters = {
          new JsonStringDefaultEnumConverter(),
          new ColumnDictionary.Converter(),
          new ColumnMerge.Converter(),
          new StringOrBoolean.JsonConverter(),
          new VariableStatusMessages.Converter(),
          new Option.JsonConverter(),
          new BooleanObjectExpandConverter<ColumnStatistics>(),
          new BooleanObjectExpandConverter<ColumnTable>(),
          new BooleanObjectExpandConverter<QuickRegistrationButtons>(),
          new BooleanObjectExpandConverter<CheckBoxForm>(),
          new BooleanObjectExpandConverter<TextForm>(),
          new BooleanObjectExpandConverter<DateForm>(),
          new BooleanObjectExpandConverter<RadioForm>(),
          new BooleanObjectExpandConverter<ComboBoxForm>(),
          new TrimStringConverter(),
        },
    };

    public static Config ReadFromJson(string path) {
      Config? settings = null;
      try {
        using Stream stream = File.OpenRead(path);
        settings = JsonSerializer.Deserialize<Config>(stream, JSON_OPTIONS);
        if (settings == null) {
          throw new Exception($"Failed to deserialize settings {path}");
        }
      } catch (JsonException e) {
        throw new JsonException(e.Message + "\n\"" + path + "\" vale JSON struktuur", e);
      } catch (FileNotFoundException) {
        settings = new();
      }

      return settings;
    }

    public enum Orientation {
      VERTICAL, HORIZONTAL
    }

    public enum Rule {
      ALLOW, CONFIRM, DENY
    }

    // TODO remove?
    //public enum ColumnResizePolicy {
    //  UNCONSTRAINED, CONSTRAINED
    //}

    public class QuickRegistrationButtons {
      public bool ShowSelectedPerson { get; init; } = true;
    }

    public class GeneralGroup {

      public string SavePath { get; init; } = "./isikreg";
      //public int SaveDelay { get; init; } = 100; // milliseconds > 0 // TODO will remove?
      //public bool SaveCompressedZip { get; init; } = true; // TODO remove, redundant now
      public bool ErrorLogging { get; init; } = true;
      public bool SmoothFont { get; init; } = true;

      public string DefaultRegistrationType { get; init; } = String.Empty;

      public Rule RegisterDuringGracePeriod { get; init; } = Rule.CONFIRM;
      public long RegisterGracePeriod { get; init; } = 10 * 60 * 1000; // >= 0, milliseconds

      public Rule RegisterSameTypeInRow { get; init; } = Rule.ALLOW;

      public bool InsertPerson { get; init; } = true;
      public bool UpdatePerson { get; init; } = true;
      public bool DeletePerson { get; init; } = true;

      public bool WarnDuplicateRegistrationDate { get; init; } = true;

      public bool TableContextMenu { get; init; } = true;

      public QuickRegistrationButtons QuickRegistrationButtons { get; init; } = new QuickRegistrationButtons();

      //public ColumnResizePolicy ColumnResizePolicy { get; init; } = ColumnResizePolicy.UNCONSTRAINED; datagrid doesn't support by default?

      // public bool CurrentSettingsMenuItem { get; init; } = false; // TODO remove??

      public VariableStatusMessages PersonDisplayFormat { get; init; } = new(
        "{PERSONAL_CODE} {FIRST_NAME} {LAST_NAME}"
      );

    }

    public class ExcelGroup {
      public string SheetName { get; init; } = "Nimekiri";
      public string ExportDateTimeFormat { get; init; } = "dd.mm.yyyy hh:mm";
      public string ExportDateFormat { get; init; } = "dd.mm.yyyy";
      //public bool ExportAutoSizeColumns { get; init; } = true; // TODO remove?
    }

    public class SmartCardGroup {

      public VariableStatusMessages StatusFormat { get; init; } = new(
          "{FIRST_NAME} {LAST_NAME}\n@event"
      );
      public int ShowSuccessStatusDuration { get; init; } = 10000; // milliseconds, negative means show success result until next card is put in

      public int ExternalTerminalFontSize { get; init; } = 50; // > 0
      public bool EnableCardPresentIndicator { get; init; } = true; // default true

      // Rules
      public Rule RegisterExpiredCards { get; init; } = Rule.CONFIRM;
      public Rule RegisterPersonNotInList { get; init; } = Rule.ALLOW; // Allow person that is not in list

      public bool QuickNewPersonRegistration { get; init; } = false; // Register new person without showing form
      public bool QuickExistingPersonRegistration { get; init; } = false;

      public long WaitForChangeLoopInterval { get; init; } = 0; // milliseconds, >= 0, How often program checks for card insert/removal changes. If 0 then program waits indefinitely until change happens

      // Card
      //public int WaitBeforeReadingCard { get; init; } = 250; // milliseconds, >= 0 TODO not using?
      public int CardReadingFailedRetryInterval { get; init; } = 2000; // milliseconds, >= 0
      public int CardReadingAttemptsUntilGiveUp { get; init; } = 4; // >= 0

      // Card Reader
      public long NoReadersCheckInterval { get; init; } = 2000; // milliseconds, >= 0
      public long ReaderMissingCheckInterval { get; init; } = 2000; // milliseconds, >= 0
      public long ReadersPresentCheckInterval { get; init; } = 10000; // milliseconds, >= 0

    }



    public GeneralGroup General { get; init; } = new();
    public ExcelGroup Excel { get; init; } = new();
    public SmartCardGroup SmartCard { get; init; } = new();

    public ColumnDictionary Columns { get; init; } = new();

    public Config() { }

    public void WriteAsJson(string path) {
      using Stream stream = File.Create(path);
      JsonSerializer.Serialize(stream, this, JSON_OPTIONS);
    }

    // TODO implements
    //public JsonElement toJsonTree() {
    //  return GSON.toJsonTree(this);
    //}

    public void Save(string path) {
      WriteAsJson(path);
    }

    public void SaveNew(string path) {
      if (!File.Exists(path)) {
        Save(path);
      }
    }

    public string? GetDefaultRegistrationType() {
      List<string> types = GetRegistrationTypes();
      if (General.DefaultRegistrationType != null && types
        .Any(t => t.ToLower().Trim().Equals(General.DefaultRegistrationType.Trim().ToLower())))
        return General.DefaultRegistrationType;
      return types.Count > 0 ? types.ElementAt(0) : null;
    }

    public List<string> GetRegistrationTypes() {
      Column? registrationTypeColumn = Columns.GetValueOrDefault(ColumnId.REGISTRATION_TYPE);
      if (registrationTypeColumn is OptionsColumn options) {
        return options.GetOptionValues();
      }
      return new List<string>();
    }

    public List<GroupedColumn> RegistrationTypeGroupColumns() {
      Column regTypeColumn = Columns[ColumnId.REGISTRATION_TYPE];
      List<string> extraColumnNames = new();
      if (regTypeColumn is OptionsColumn optRegTypeColumn) {
        extraColumnNames.AddRange(optRegTypeColumn.GetOptionValues());
      } else {
        extraColumnNames.Add(regTypeColumn.Label);
      }

      return extraColumnNames.SelectMany(label => Columns
      .Where(c => c.Group == ColumnGroup.REGISTRATION && c.Id != ColumnId.REGISTERED && c.Id != ColumnId.REGISTRATION_TYPE)
      .Select((Column regCol) => {
        return new GroupedColumn(label, regCol) {
          Label = (label + " " + regCol.Label).FirstCharCapitalize(),
        };
      })).ToList();
    }

  }
}
