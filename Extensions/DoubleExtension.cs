using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsikReg.Extensions {
  public static class DoubleExtension {

    public static double Bound(this double value, double min, double max) {
      if (value < min) return min;
      if (value > max) return max;
      return value;
    }

  }
}
