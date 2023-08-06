using IsikReg.Configuration;
using IsikReg.Properties;
using IsikReg.SmartCards;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

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
