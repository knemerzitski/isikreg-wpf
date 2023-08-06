using System;
using System.Collections;
using System.Collections.Generic;

namespace IsikReg.Collections {

  // TODO test this enumerator works
  public class ConcatEnumerator<T> : IEnumerator<T> {

    private readonly IEnumerator<T>[] enumerators;
    private int index = 0;
    private IEnumerator<T> Enumerator => enumerators[index];

    public ConcatEnumerator(params IEnumerator<T>[] enumerators) {
      this.enumerators = enumerators;
    }

    public T Current => Enumerator.Current;
    object? IEnumerator.Current => Current;

    public bool MoveNext() {
      while (true) {
        if (Enumerator.MoveNext()) {
          return true;
        } else {
          if (index + 1 < enumerators.Length) {
            index++;
          } else {
            break;
          }
        }
      }
      return false;
    }

    public void Reset() {
      foreach(var e in enumerators) {
        e.Reset();
      }
      index = 0;
    }

    public void Dispose() {
      GC.SuppressFinalize(this);
    }

  }
}
