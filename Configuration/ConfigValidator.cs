using IsikReg.Configuration.Columns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IsikReg.Configuration {
  public static class ConfigValidator {

    public static void Validate(Config settings) {
      // TODO deny all columns with ID starting with underscore!!!!!

      // General
      ValidateNonNegative("settings.general.registerGracePeriod", settings.General.RegisterGracePeriod);
      ValidateRequired("settings.smartCard.statusFormat", settings.SmartCard.StatusFormat);
      //ValidatePositive("settings.general.saveDelay", settings.General.SaveDelay); // TODO removed?
      ValidateRequired("settings.general.savePath", settings.General.SavePath);

      // SmartCard
      ValidatePositive("settings.smartCard.externalTerminalFontSize", settings.SmartCard.ExternalTerminalFontSize);

      ValidateNonNegative("settings.smartCard.noReadersCheckInterval", settings.SmartCard.NoReadersCheckInterval);
      ValidateNonNegative("settings.smartCard.readerMissingCheckInterval", settings.SmartCard.ReaderMissingCheckInterval);
      ValidateNonNegative("settings.smartCard.readersPresentCheckInterval", settings.SmartCard.ReadersPresentCheckInterval);

      ValidateNonNegative("settings.smartCard.waitForChangeLoopInterval", settings.SmartCard.WaitForChangeLoopInterval);
      //ValidateNonNegative("settings.smartCard.waitBeforeReadingCard", settings.SmartCard.WaitBeforeReadingCard); // TODO not using?
      ValidateNonNegative("settings.smartCard.cardReadingFailedRetryInterval", settings.SmartCard.CardReadingFailedRetryInterval);
      ValidateNonNegative("settings.smartCard.cardReadingAttemptsUntilGiveUp", settings.SmartCard.CardReadingAttemptsUntilGiveUp);

      // TODO columns validate key, label unique

      // Columns
      var columns = settings.Columns;
      Column? columnEmptyLabel = columns.Where(c => string.IsNullOrWhiteSpace(c.Label)).FirstOrDefault();
      if (columnEmptyLabel != null) {
        ThrowException("Peab olema täidetud", "column.label", columnEmptyLabel.Label);
      }

      ValidateColumnRequired(settings, ColumnId.REGISTERED);
      ValidateColumnGroup(settings, ColumnId.REGISTERED, ColumnGroup.REGISTRATION);
      ValidateColumnType(settings, ColumnId.REGISTERED, typeof(CheckBoxColumn));
      ValidateColumnFormNotAllowed(settings, ColumnId.REGISTERED);
      //ValidateColumnStatisticsFalse(settings, ColumnId.REGISTERED);

      ValidateColumnRequired(settings, ColumnId.REGISTRATION_TYPE);
      ValidateColumnGroup(settings, ColumnId.REGISTRATION_TYPE, ColumnGroup.REGISTRATION);
      ValidateColumnTypeFixedOptions(settings, ColumnId.REGISTRATION_TYPE);
      ValidateColumnValueRequired(settings, ColumnId.REGISTRATION_TYPE);
      ValidateColumnStatisticsFalse(settings, ColumnId.REGISTRATION_TYPE);

      ValidateColumnRequired(settings, ColumnId.REGISTER_DATE);
      ValidateColumnGroup(settings, ColumnId.REGISTER_DATE, ColumnGroup.REGISTRATION);
      ValidateColumnType(settings, ColumnId.REGISTER_DATE, typeof(DateColumn));
      ValidateColumnTableEditableFalse(settings, ColumnId.REGISTER_DATE);
      ValidateColumnStatisticsFalse(settings, ColumnId.REGISTER_DATE);

      ValidateColumnRequired(settings, ColumnId.PERSONAL_CODE);
      ValidateColumnGroup(settings, ColumnId.PERSONAL_CODE, ColumnGroup.PERSON);
      ValidateColumnAnyValue(settings, ColumnId.PERSONAL_CODE);
      ValidateColumnValueRequired(settings, ColumnId.PERSONAL_CODE);
      ValidateColumnStatisticsFalse(settings, ColumnId.PERSONAL_CODE);

      Column? columnEmptyOptionsLabel = columns.Where(c => c is OptionsColumn o && o.Options.Any(v => string.IsNullOrWhiteSpace(v.Label))).FirstOrDefault();
      if (columnEmptyOptionsLabel != null) {
        ThrowException("Peab olema täidetud", "column.options.label", columnEmptyOptionsLabel.Label);
      }

      ValidateNoDuplicates("column.id", columns.Where(c => c.Id != ColumnId.NULL).Select(c => c.Id).ToList());
      ValidateNoDuplicates("column.label", columns.Select(c => ColumnDictionary.ToKey(c.Label)).ToList());
    }

    private static void ValidateColumnTypeFixedOptions(Config config, ColumnId id) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col is not OptionsColumn) {
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"type\" peab olema \"RADIO\" või \"COMBOBOX\"!", id));
      }
      if (col is ComboBoxColumn combo && combo.Form != null && combo.Form.Editable) {
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"form.editable\" peab olema false!", id));
      }
    }

    private static void ValidateColumnAnyValue(Config config, ColumnId id) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col is TextColumn) return;
      if (col is ComboBoxColumn && col is ComboBoxColumn combo) {
        if (combo.Form != null && combo.Form.Editable) return;
      }
      if (col is OptionsColumn options) {
        if (options.GetOptionValues().Count == 0) {
          throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"options\" peab sisaldama vähemalt ühte väärtust!", id));
        }
        return;
      }
      throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"type\" peab olema \"TEXT\", \"COMBOBOX\" või \"RADIO\"!", id));
    }

    private static void ValidateColumnStatisticsFalse(Config config, ColumnId id) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col == null || col.Statistics != null)
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"statistics\" peab olema false!", id));
    }

    private static void ValidateColumnType(Config config, ColumnId id, Type type) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col == null || col.GetType() != type)
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"type\" peab olema \"{1}\"!", id, type));
    }

    private static void ValidateColumnGroup(Config config, ColumnId id, ColumnGroup group) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col == null || col.Group != group)
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"group\" peab olema \"{1}\"!", id, group));
    }

    private static void ValidateColumnTableEditableFalse(Config config, ColumnId id) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col == null || (col.Table?.Editable ?? false))
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"table.editable\" peab olema false!", id));
    }

    private static void ValidateColumnValueRequired(Config config, ColumnId id) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col == null || !col.Required)
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"required\" peab olema true!", id));
    }

    private static void ValidateColumnFormNotAllowed(Config config, ColumnId id) {
      Column? col = config.Columns.GetValueOrDefault(id);
      if (col == null || col.HasForm())
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" \"form\" peab olema false!", id));
    }

    private static void ValidateColumnRequired(Config config, ColumnId id) {
      if (!config.Columns.Contains(id)) {
        throw new SettingsValidationException(string.Format("Veerg ID \"{0}\" on kohustuslik!", id));
      }
    }

    private static void ValidateRequired(string name, object? value) {
      if (value == null || value is string str && string.IsNullOrWhiteSpace(str))
        ThrowException("Peab olema täidetud", name, value);
    }

    private static void ValidateNonNegative<T>(string name, T value) {
      if (Convert.ToInt32(value) < 0)
        ThrowException("Ei tohi olla negatiivne", name, value);
    }

    private static void ValidatePositive<T>(string name, T value) where T : IComparable {
      if (Convert.ToInt32(value) <= 0)
        ThrowException("Peab olema positiivne", name, value);
    }


    private static void ValidateNoDuplicates<T>(string name, List<T> list) {
      for (int i = 0; i < list.Count; i++) {
        T li = list.ElementAt(i);
        if (li == null)
          continue;
        for (int j = 0; j < list.Count; j++) {
          if (i == j)
            continue;
          T lj = list.ElementAt(j);
          if (lj == null)
            continue;
          if (li.Equals(lj))
            ThrowException("Peab olema unikaalne", name, li);
        }
      }
    }

    private static void ThrowException(string message, string name, object? value, params object[] args) {
      if (value is string)
        value = "\"" + value + "\"";
      throw new SettingsValidationException(string.Format("Seadistuse viga \"{0}\": {1}\n" + message, name, value, args));
    }


    public class SettingsValidationException : Exception {
      public SettingsValidationException() {
      }

      public SettingsValidationException(string message) : base(message) {
      }
    }



  }
}
