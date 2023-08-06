using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.Extensions {
  public static class CollectionExtension {

    public static void ForEach<T>(this ObservableCollection<T> collection, Action<T> action) {
      foreach (T value in collection) {
        action.Invoke(value);
      }
    }

  }
}
