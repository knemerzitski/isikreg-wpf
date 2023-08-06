using System.Windows;
using System.Windows.Controls;

namespace IsikReg.Extensions {
  public static class UIElementCollectionExtension {

    public static void SetGap(this UIElementCollection elements, double horizontal = 0, double vertical = 0) {
      for (int i = 1; i < elements.Count; i++) {
        if (elements[i] is FrameworkElement element) {
          element.Margin = new Thickness(horizontal, vertical, 0, 0);
        }
      }
    }

  }
}
