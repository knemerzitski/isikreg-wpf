using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IsikReg.Extensions {
  public static class DictionaryExtension {

    public static T? GetValueOrDefault<T>(this IReadOnlyDictionary<string, T> dict, ColumnId id) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key == null) return default;
      return dict.GetValueOrDefault(key);
    }

    public static T? GetValueOrDefault<T>(this IReadOnlyDictionary<string, T> dict, Column col) {
      return dict.GetValueOrDefault(col.Key);
    }

    //public static void SetIfExists<T>(this IDictionary<string, T> dict, ColumnId id, T value) {
    //  string key = id.ToString();
    //  if (dict.ContainsKey(key)) {
    //    dict[key] = value;
    //  }
    //}
    //public static void SetIfExists<T>(this IDictionary<string, T> dict, Column col, T value) {
    //  string key = col.Key;
    //  if (dict.ContainsKey(key)) {
    //    dict[key] = value;
    //  }
    //}

    public static string GetString<TValue>(this IReadOnlyDictionary<string, TValue> dict, ColumnId id) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key == null) return string.Empty;
      if (dict.TryGetValue(key, out TValue? value)) {
        return value is string str ? str : string.Empty;
      }
      return string.Empty;
    }

    public static bool GetBool<TValue>(this IReadOnlyDictionary<string, TValue> dict, ColumnId id) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key == null) return false;
      if (dict.TryGetValue(key, out TValue? value)) {
        return value is bool b && b;
      }
      return false;
    }

    public static DateTime? GetDateTime<TValue>(this IReadOnlyDictionary<string, TValue> dict, ColumnId id) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key == null) return null;
      if (dict.TryGetValue(key, out TValue? value)) {
        return value is DateTime date ? date : null;
      }
      return null;
    }

    public static string GetPersonalCode<TValue>(this IReadOnlyDictionary<string, TValue> dict) {
      return dict.GetString(ColumnId.PERSONAL_CODE);
    }

    public static string GetRegistrationType<TValue>(this IReadOnlyDictionary<string, TValue> dict) {
      return dict.GetString(ColumnId.REGISTRATION_TYPE);
    }

    public static DateTime? GetRegisteredDate<TValue>(this IReadOnlyDictionary<string, TValue> dict) {
      return dict.GetDateTime(ColumnId.REGISTER_DATE);
    }

    public static DateTime? GetExpiryDate<TValue>(this IReadOnlyDictionary<string, TValue> dict) {
      return dict.GetDateTime(ColumnId.EXPIRY_DATE);
    }

    public static string GetPersonDisplayInfo<TValue>(this IReadOnlyDictionary<string, TValue> dict) {
      return Config.Instance.General.PersonDisplayFormat.ToString(dict.ValuesAsString());
    }

    public static IReadOnlyDictionary<string, T> GetGroup<T>(this IEnumerable<KeyValuePair<string, T>> dict, ColumnGroup group) {
      Dictionary<string,T> result = new();
      foreach ((var key, var value) in dict) {
        if (Config.Instance.Columns.TryGetValue(ColumnDictionary.ToKey(key), out Column? column) && column.Group == group) {
          result.Add(column.Key, value);
        }
      }
      return result;
    }

    public static bool AnyGroup<T>(this IEnumerable<KeyValuePair<string, T>> dict, ColumnGroup group) {
      return dict.Any(e => {
        if (Config.Instance.Columns.TryGetValue(ColumnDictionary.ToKey(e.Key), out Column? column)) {
          return column.Group == group;
        }
        return false;
      });
    }



    public static bool Set<T>(this IDictionary<string, T> dict, ColumnId id, T value) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key == null) return false;
      dict[key] = value;
      return true;
    }

    public static void Set<T>(this IDictionary<string, T> dict, Column col, T value) {
      dict[col.Key] = value;
    }

    public static void SetRange<TKey, TValue>(this IDictionary<TKey, TValue> dict, IEnumerable<KeyValuePair<TKey, TValue>> other) where TKey : notnull {
      foreach ((TKey key, TValue value) in other) {
        dict[key] = value;
      }
    }

    public static void Remove<T>(this IDictionary<string, T> dict, ColumnId id) {
      string? key = Config.Instance.Columns.GetValueOrDefault(id)?.Key;
      if (key == null) return;
      dict.Remove(key);
    }

    public static void Remove<T>(this IDictionary<string, T> dict, Column col) {
      dict.Remove(col.Key);
    }

    public static Dictionary<TKey, string> ValuesAsString<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> dict) where TKey : notnull {
      return dict.ToDictionary(e => e.Key, e => {
        return e.Value?.ToString() ?? string.Empty;
      });
    }

    public static void Merge(this IDictionary<string, object> dict, IReadOnlyDictionary<string, object> fromDict) {
      foreach ((string key, object newValue) in fromDict) {
        Column? column = Config.Instance.Columns.GetValueOrDefault(key);
        if (column == null) continue;
        object? value = dict.TryGetValue(column.Key, out object? v) ? v : null;
        if (column.GetValueType().Equals(typeof(DateTime))) {
          DateTime? newDate = newValue is DateTime d2 ? d2 : null;
          if (newDate == null) continue;
          DateTime? date = value is DateTime d1 ? d1 : null;
          switch (column.Merge.Rule) {
            case ColumnMerge.RuleType.OVERWRITE_ON_EMPTY:
              if (date == null) {
                dict[column.Key] = newDate;
              }
              break;
            case ColumnMerge.RuleType.NEWER:
              if (date == null) {
                dict[column.Key] = newDate;
              } else if (newDate > date) {
                dict[column.Key] = newDate;
              }
              break;
            case ColumnMerge.RuleType.OLDER:
              if (date == null) {
                dict[column.Key] = newDate;
              } else if (date > newDate) {
                dict[column.Key] = newDate;
              }
              break;
          }
        } else if (column.GetValueType().Equals(typeof(string))) {
          string? newStr = newValue is string s2 ? s2 : null;
          if (newStr == null) continue;
          string? str = value is string s1 ? s1 : null;
          switch (column.Merge.Rule) {
            case ColumnMerge.RuleType.OVERWRITE_ON_EMPTY:
              if (string.IsNullOrWhiteSpace(str)) {
                dict[column.Key] = newStr;
              }
              break;
            case ColumnMerge.RuleType.COMBINE:
              string[] values = str?.Split(column.Merge.Separator.Trim()) ?? Array.Empty<string>();
              string[] newValues = newStr?.Split(column.Merge.Separator.Trim()) ?? Array.Empty<string>();
              dict[column.Key] = string.Join(column.Merge.Separator, values.Concat(newValues)
                .Select(v => v.Trim()).Distinct().Where(v => v.Length > 0).ToList());
              break;
          }
        } else if (column.GetValueType().Equals(typeof(bool))) {
          // booleanProperty.Value = (booleanProperty.Value ?? false) || (newBooleanProperty.Value ?? false);
          bool? newBoolean = newValue is bool b2 ? b2 : null;
          if (newBoolean == null) continue;
          bool? boolean = value is bool b1 ? b1 : null;
          switch (column.Merge.Rule) {
            case ColumnMerge.RuleType.OVERWRITE_ON_EMPTY:
              if (boolean == null) {
                dict[column.Key] = newBoolean;
              }
              break;
          }
        }
      }
    }

    //public static void SetIfExists<TKey>(this IDictionary<TKey, object?> dict, IReadOnlyDictionary<TKey, object?> other) where TKey : notnull {
    //  foreach ((TKey key, object? value) in other) {
    //    if (dict.ContainsKey(key)) {
    //      dict[key] = value;
    //    }
    //  }
    //}

    public static void SetIfExists<TKey, TValue>(this IReadOnlyDictionary<TKey, IProperty> dict, IEnumerable<KeyValuePair<TKey, TValue>> other) where TKey : notnull {
      foreach ((TKey key, TValue newValue) in other) {
        IProperty? property = dict.GetValueOrDefault(key);
        property?.TrySet(newValue);
      }
    }

    public static void UpdateFormAutoFillIndex(this IReadOnlyDictionary<string, object> dict) {
      foreach ((string key, object value) in dict) {
        Column? column = Config.Instance.Columns.GetValueOrDefault(key);
        if (column == null) continue;

        if (column is ComboBoxColumn comboBoxColumn) {
          // Update COMBOBOX autofill values
          if (comboBoxColumn.Form != null && (comboBoxColumn.Form.autofillPattern != null || comboBoxColumn.Form.IsSimpleAutofill() &&
            value is string)) {
            if (comboBoxColumn.Form.autoFillSelected == 2) {
              comboBoxColumn.Form.autoFillSelected = 1;
            }

            string? str = value as string;
            if (string.IsNullOrEmpty(str))
              return;

            // Update autofill index
            Regex? pattern = comboBoxColumn.Form.autofillPattern;
            if (pattern != null) {
              Match matcher = pattern.Match(str);
              if (matcher.Success && matcher.Groups.Count > 0) { // TODO check finds group correctly
                string digitStr = matcher.Groups[1].Value;
                if (int.TryParse(digitStr, out int index)) {
                  if (comboBoxColumn.Form.autofillIndex <= index) {
                    comboBoxColumn.Form.autofillIndex = index + 1;
                  }
                }
              }
            }
            if (!comboBoxColumn.Form.autofillValues.Contains(str) && !comboBoxColumn.GetOptionValues().Contains(str)) {
              comboBoxColumn.Form.autofillValues.Insert(0, str);
            }
            if (comboBoxColumn.Form.autoFillUpdateUsePrevious) {
              comboBoxColumn.Form.autoFillPrevious = str;
            }
          }
        }
      }
    }

    //public static bool Same<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, IReadOnlyDictionary<TKey, TValue> other) where TKey : notnull {
    //  if (dict == other) return true;

    //  if (dict.Keys.Count() != other.Keys.Count()) return false; // Key count mismatch
    //  if (dict.Keys.Except(other.Keys).Any()) return false; // Contains different keys

    //  return dict.Keys.All((TKey key) => {
    //    TValue? o1 = dict.GetValueOrDefault(key);
    //    TValue? o2 = other.GetValueOrDefault(key);
    //    return o1 == null && o2 == null || (o1 != null && o1.Equals(o2));
    //  });
    //}

    public static void RemoveEmptyStrings<TKey, TValue>(this IDictionary<TKey, TValue> dict) {
      TKey[] keys = dict.Where(e => e.Value is string s && string.IsNullOrWhiteSpace(s)).Select(e => e.Key).ToArray();
      foreach (TKey key in keys) {
        dict.Remove(key);
      }
    }

    public static string ToDisplayString<TValue>(this IReadOnlyDictionary<string, TValue> dict) {
      return string.Join("; ", dict.Select(e => e.Key + ": " + e.Value).ToList());
    }

  }
}
