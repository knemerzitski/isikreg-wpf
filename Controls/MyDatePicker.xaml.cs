using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IsikReg.Controls {
  /// <summary>
  /// Interaction logic for MyDatePicker.xaml
  /// </summary>
  public partial class MyDatePicker : DatePicker {

    private static readonly DependencyProperty PlaceholderProperty =
      DependencyProperty.Register("Placeholder", typeof(string),
      typeof(MyDatePicker), new PropertyMetadata(default(string)));

    // TODO textbox padding..
    public string Placeholder {
      get { return (string)GetValue(PlaceholderProperty); }
      set { SetValue(PlaceholderProperty, value); }
    }

    public MyDatePicker() {
      InitializeComponent();
    }

  }
}
