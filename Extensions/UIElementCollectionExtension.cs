using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

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
