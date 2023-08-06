using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace IsikReg.Converters {
  public class RadioGroupConverter : IValueConverter {


    private readonly RadioButton target;

    public RadioGroupConverter(RadioButton target) {
      this.target = target;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return target.Content.Equals(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      return target.Content;
    }
  }
}
