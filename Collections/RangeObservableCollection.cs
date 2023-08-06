using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.Collections {
  public class RangeObservableCollection<T> : ObservableCollection<T> {

    public static readonly PropertyChangedEventArgs CountPropertyChanged = new PropertyChangedEventArgs("Count");
    public static readonly PropertyChangedEventArgs IndexerPropertyChanged = new PropertyChangedEventArgs("Item[]");
    public static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

    private bool suppressCollectionChanged = false;

    public bool SuppressCollectionChanges {
      get => suppressCollectionChanged; 
      set {
        bool old = suppressCollectionChanged;
        suppressCollectionChanged = value;
        if (old && !suppressCollectionChanged) {
          OnPropertyChanged(CountPropertyChanged);
          OnPropertyChanged(IndexerPropertyChanged);
          OnCollectionChanged(ResetCollectionChanged);
        }
      }
    }

    public RangeObservableCollection() {
    }

    public RangeObservableCollection(IEnumerable<T> collection) : base(collection) {
    }

    public RangeObservableCollection(List<T> list) : base(list) {
    }

    public void AddRange(IEnumerable<T> items) {
      SuppressCollectionChanges = true;

      foreach (T item in items) {
        Add(item);
      }

      SuppressCollectionChanges = false;
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
      if (!suppressCollectionChanged) base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
      if (!suppressCollectionChanged) base.OnPropertyChanged(e);
    }





  }
}
