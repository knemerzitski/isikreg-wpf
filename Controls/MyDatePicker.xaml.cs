using System.Windows;
using System.Windows.Controls;

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
