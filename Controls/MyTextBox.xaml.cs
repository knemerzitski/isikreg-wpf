using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
