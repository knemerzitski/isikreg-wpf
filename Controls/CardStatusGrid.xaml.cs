using IsikReg.Configuration;
using IsikReg.Properties;
using IsikReg.SmartCards;
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
  /// Interaction logic for CardStatusGrid.xaml
  /// </summary>
  /// 


  public partial class CardStatusGrid : Grid {

    public CardStatusGrid() {
      InitializeComponent();


    }

    public void BindStatusText(CardStatusText statusText) {
      if (!Config.Instance.SmartCard.EnableCardPresentIndicator) {
        RemoveCardIcon();
      } else {
        BindCardPresent(statusText.CardPresentProperty);
      }
      BindText(statusText.TextProperty);
      BindColor(statusText.ColorProperty);
    }

    public void BindText(Property<string> property) {
      CardText.SetBinding(TextBlock.TextProperty, new Binding("Value") {
        Source = property,
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
      });
    }

    public void BindColor(Property<Brush> property) {
      CardText.SetBinding(TextBlock.ForegroundProperty, new Binding("Value") {
        Source = property,
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
      });
    }

    public void BindCardPresent(Property<bool> property) {
      if (!Children.Contains(CardIcon)) return;

      CardIcon.SetBinding(Image.VisibilityProperty, new Binding("Value") {
        Source = property,
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        Converter = new VisibilityBoolConverter(),
      });
    }

    public void RemoveCardIcon() {
      Children.Remove(CardIcon);
      CardIcon.ClearValue(Image.VisibilityProperty);
    }
  }
}
