using System.Windows;
using System.Windows.Controls;

namespace IsikReg.Controls {
  /// <summary>
  /// Interaction logic for MyTextBox.xaml
  /// </summary>
  public partial class MyTextBox : TextBox {

    private static DependencyProperty PlaceholderProperty =
      DependencyProperty.Register("Placeholder", typeof(string),
        typeof(MyTextBox), new PropertyMetadata(default(string)));

    private static DependencyProperty CornerRadiusProperty =
      DependencyProperty.Register("CornerRadius", typeof(CornerRadius),
    typeof(MyTextBox), new PropertyMetadata(default(CornerRadius)));

    public string Placeholder {
      get { return (string)GetValue(PlaceholderProperty); }
      set { SetValue(PlaceholderProperty, value); }
    }

    public CornerRadius CornerRadius {
      get { return (CornerRadius)GetValue(CornerRadiusProperty); }
      set { SetValue(CornerRadiusProperty, value); }
    }

    public MyTextBox() {
      InitializeComponent();
    }
  }
}
