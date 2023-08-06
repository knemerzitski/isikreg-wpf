using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace IsikReg.Properties {

  public delegate void ValueChangedEventHandler<T>(T? oldValue, T? newValue);
  public delegate void ValueChangedEventHandler(object? oldValue, object? newValue);

  public interface IReadOnlyProperty : INotifyPropertyChanged {

    new event ValueChangedEventHandler? PropertyChanged;

    object? Value { get; }

    IProperty Copy();

  }
  public interface IProperty : IReadOnlyProperty {

    new object? Value { get; set; }

    bool TrySet(object? obj);
  }

  public interface IReadOnlyProperty<T> : IReadOnlyProperty {

    new event ValueChangedEventHandler<T>? PropertyChanged;

    new T? Value { get; }

  }

  public interface IProperty<T> : IReadOnlyProperty<T>, IProperty {

    new T? Value { get; set; }

    void Bind(IReadOnlyProperty<T> newProperty);

    void Unbind();

    public IBinding? Binding { get; set; }

  }

  public class Property<T> : IProperty<T> {

    public static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));

    public event ValueChangedEventHandler<T>? PropertyChanged;

    private readonly Dictionary<object, ValueChangedEventHandler<T>> propertyChangedConverters = new();
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged {
      add {
        if (value == null || propertyChangedConverters.ContainsKey(value)) return;
        propertyChangedConverters[value] = (o, n) => {
          value?.Invoke(this, ValueChanged);
        }; ;
        PropertyChanged += propertyChangedConverters[value];
      }
      remove {
        if (value == null) return;
        PropertyChanged -= propertyChangedConverters[value];
        propertyChangedConverters.Remove(value);
      }
    }

    event ValueChangedEventHandler? IReadOnlyProperty.PropertyChanged {
      add {
        if (value == null || propertyChangedConverters.ContainsKey(value)) return;
        propertyChangedConverters[value] = (o, n) => {
          value?.Invoke(o, n);
        }; ;
        PropertyChanged += propertyChangedConverters[value];
      }

      remove {
        if (value == null) return;
        PropertyChanged -= propertyChangedConverters[value];
        propertyChangedConverters.Remove(value);
      }
    }

    private T? value;

    public T? Value {
      get => value;
      set {
        T? oldValue = this.value;
        if(value == null && oldValue != null || value != null && !value.Equals(oldValue)) {
          this.value = value;
          OnPropertyChanged(oldValue, value);
        }
      }
    }

    object? IReadOnlyProperty.Value => Value;

    object? IProperty.Value {
      get => Value;
      set => TrySet(value);
    }

    private IReadOnlyProperty<T>? boundProperty;


    public bool IsBound { get => boundProperty != null || Binding != null; }
    public IBinding? Binding { get; set; }

    public Property() {
      Value = default;
    }

    public Property(T? value = default) {
      this.value = value;
    }

    protected void OnPropertyChanged(T? oldValue, T? newValue) {
      PropertyChanged?.Invoke(oldValue, newValue);
    }

    public void Bind(IReadOnlyProperty<T> newProperty) {
      if (boundProperty == newProperty || this == newProperty) {
        return;
      }
      Unbind();

      Value = newProperty.Value;

      boundProperty = newProperty;
      boundProperty.PropertyChanged += BoundValueChanged;
    }

    public void Unbind() {
      if (boundProperty != null) {
        Value = boundProperty.Value;

        boundProperty.PropertyChanged -= BoundValueChanged;
        boundProperty = null;
      }
      if (Binding != null) {
        Binding.Unbind();
        Binding = null;
      }
    }


    public void BindBidirectional(IProperty<T> property) {
      if (property == this) return;
      Unbind();
      property.Unbind();

      Bind(property);
      property.Bind(this);
    }

    public void BindBidirectional<S>(IProperty<S> property,
      PropertyConverter<T?, S?> tConverter,
      PropertyConverter<S?, T?> sConverter, bool
      updateFirst = true) {
      if (Binding != null && Binding.IsBoundTo(this, property)) {
        return;
      }
      Unbind();
      property.Unbind();

      Binding = new Binding<T, S>(this, property, tConverter, sConverter, updateFirst);
      property.Binding = Binding;
    }

    private void BoundValueChanged(T? oldValue, T? newValue) {
      Value = newValue;
    }

    public bool TrySet(object? obj) {
      object? val = obj is IReadOnlyProperty p ? p.Value : obj;
      if (val is T tval) {
        Value = tval;
        return true;
      }
      return false;
    }

    public IProperty Copy() {
      return new Property<T?>(Value);
    }

    public override string ToString() {
      object val = Value != null ? Value : "NULL";
      return $"Property: {val}";
    }


  }

}
