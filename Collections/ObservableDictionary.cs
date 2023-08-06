using IsikReg.Configuration;
using IsikReg.Configuration.Columns;
using IsikReg.Extensions;
using IsikReg.Model;
using IsikReg.Properties;
using NPOI.POIFS.Crypt;
using NPOI.SS.Formula.Functions;
using NPOI.XSSF.Streaming.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace IsikReg.Collections {

  public enum DictionaryAction {
    CHANGED,
    ADDED,
    REMOVED
  }

  public delegate void DictionaryChangedEventHandler<TKey, TValue>(TKey key, TValue? oldValue, TValue? newValue, DictionaryAction action);

  public class ObservableDictionary<T> : ObservableDictionary<string, T>, INotifyPropertyChanged {

    public ObservableDictionary() {
    }

    public ObservableDictionary(IEnumerable<KeyValuePair<string, T>> properties) : base(properties) {
    }

    public new virtual T? this[string key] {
      get => GetValueOrDefault(key); set {
        if (value != null) {
          base[key] = value;
        } else {
          Remove(key);
        }
      }
    }


    #region INotifyPropertyChanged

    private static readonly PropertyChangedEventArgs INDEXER_EVENT = new("Item[]");
    private readonly Dictionary<object, DictionaryChangedEventHandler<string, T>> propertyChangedConverters = new();
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged {
      add {
        if (value == null || propertyChangedConverters.ContainsKey(value)) return;
        propertyChangedConverters[value] = (key, o, n, a) => {
          value?.Invoke(this, INDEXER_EVENT);
        };
        PropertyChanged += propertyChangedConverters[value];
      }
      remove {
        if (value == null) return;
        PropertyChanged -= propertyChangedConverters[value];
        propertyChangedConverters.Remove(value);
      }
    }

    #endregion

  }

  public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> where TKey : notnull {

    private readonly Dictionary<TKey, TValue> properties;

    public int Count => properties.Count;

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    public IEnumerable<TKey> Keys => properties.Keys;

    public IEnumerable<TValue> Values => properties.Values;

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => properties.Keys;

    ICollection<TValue> IDictionary<TKey, TValue>.Values => properties.Values;

    public virtual TValue this[TKey key] {
      get => properties[key];
      set {
        if (properties.TryGetValue(key, out TValue? oldValue)) {
          if (value == null && oldValue != null || value != null && !value.Equals(oldValue)) {
            properties[key] = value;
            OnPropertyChanged(key, oldValue, value, DictionaryAction.CHANGED);
          }
        } else {
          properties[key] = value;
          OnPropertyChanged(key, default, value, DictionaryAction.ADDED);
        }
      }
    }

    public event DictionaryChangedEventHandler<TKey, TValue>? PropertyChanged;

    public ObservableDictionary() {
      properties = new();
    }

    public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> properties) {
      this.properties = new(properties);
    }

    protected void OnPropertyChanged(TKey propertyName, TValue? oldValue, TValue? newValue, DictionaryAction action) {
      PropertyChanged?.Invoke(propertyName, oldValue, newValue, action);
    }

    public void Add(TKey key, TValue value) {
      properties.Add(key, value);
      OnPropertyChanged(key, default, value, DictionaryAction.ADDED);
    }

    public bool ContainsKey(TKey key) {
      return properties.ContainsKey(key);
    }

    public bool Remove(TKey key) {
      TValue? oldValue = properties.GetValueOrDefault(key);
      if (properties.Remove(key)) {
        OnPropertyChanged(key, oldValue, default, DictionaryAction.REMOVED);
        return true;
      }
      return false;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
      return properties.TryGetValue(key, out value);
    }

    public TValue? GetValueOrDefault(TKey key, TValue? defaultValue = default) {
      return properties.TryGetValue(key, out TValue? value) ? value : defaultValue;
    }

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void Clear() {
      Dictionary<TKey, TValue> cpy = new(properties);
      properties.Clear();
      foreach ((var key, var value) in cpy) {
        OnPropertyChanged(key, value, default, DictionaryAction.REMOVED);
      }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
      return properties.Contains(item);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
      ((ICollection<KeyValuePair<TKey, TValue>>)properties).CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) {
      if (((ICollection<KeyValuePair<TKey, TValue>>)properties).Remove(item)) {
        OnPropertyChanged(item.Key, item.Value, default, DictionaryAction.REMOVED);
        return true;
      }
      return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
      return properties.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return properties.GetEnumerator();
    }

  }
}
