using System.ComponentModel;

namespace IsikReg.Properties {

  public delegate S PropertyConverter<T, S>(T value);
  public interface IBinding {

    bool IsBoundTo(IProperty property1, IProperty property2);
    void Unbind();

  }

  public class Binding<T, S> : IBinding {

    private readonly IProperty<T> tProperty;
    private readonly IProperty<S> sProperty;
    private readonly PropertyConverter<T?, S?> tConverter;
    private readonly PropertyConverter<S?, T?> sConverter;

    private readonly object changingLock = new();
    private bool changing = false;

    public Binding(IProperty<T> tProperty, IProperty<S> sProperty, PropertyConverter<T?, S?> tConverter, PropertyConverter<S?, T?> sConverter, bool updateFirst = true) {
      this.tProperty = tProperty;
      this.sProperty = sProperty;
      this.tConverter = tConverter;
      this.sConverter = sConverter;

      CancelEventArgs cancel = new();
      if (updateFirst) {
        SPropertyChanged(sProperty.Value, sProperty.Value);
      } else {
        TPropertyChanged(tProperty.Value, tProperty.Value);
      }

      tProperty.PropertyChanged += TPropertyChanged;
      sProperty.PropertyChanged += SPropertyChanged;
    }

    ~Binding() {
      Unbind();
    }

    private void TPropertyChanged(T? oldValue, T? newValue) {
      lock (changingLock) {
        if (changing) return;
        changing = true;
        sProperty.Value = tConverter.Invoke(newValue);
        changing = false;
      }
    }

    private void SPropertyChanged(S? oldValue, S? newValue) {
      lock (changingLock) {
        if (changing) return;
        changing = true;
        tProperty.Value = sConverter.Invoke(newValue);
        changing = false;
      }
    }

    public bool IsBoundTo(IProperty property1, IProperty property2) {
      return tProperty == property1 && sProperty == property2
        || tProperty == property2 && sProperty == property1;
    }

    public void Unbind() {
      tProperty.PropertyChanged -= TPropertyChanged;
      sProperty.PropertyChanged -= SPropertyChanged;
      tProperty.Binding = null;
      sProperty.Binding = null;
    }


  }
}
