using System;
using System.Collections.ObjectModel;

namespace IsikReg.Extensions {
  public static class CollectionExtension {

    public static void ForEach<T>(this ObservableCollection<T> collection, Action<T> action) {
      foreach (T value in collection) {
        action.Invoke(value);
      }
    }

  }
}
