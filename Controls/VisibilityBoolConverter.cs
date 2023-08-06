using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IsikReg.Controls {
  internal class VisibilityBoolConverter : IValueConverter {

    private readonly Visibility falseVisibility;

    public VisibilityBoolConverter(Visibility falseVisibility = Visibility.Hidden) {
      this.falseVisibility = falseVisibility;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return value is bool b && b ? Visibility.Visible : falseVisibility;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      return value is Visibility v && v == Visibility.Visible;
    }
  }
}
