using System;

namespace IsikReg.Extensions {
  public static class FunctionExtension {

    public static Func<I, O> Compose<I, M, O>(this Func<I, M> f, Func<M, O> g) {
      return x => g(f(x));
    }

    public static Func<I, I2, O> Compose<I, I2, M, O>(this Func<I, I2, M> f, Func<M, O> g) {
      return (x, y) => g(f(x, y));
    }

  }
}
