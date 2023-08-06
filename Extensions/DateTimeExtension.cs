using System;

namespace IsikReg.Extensions {
  public static class DateTimeExtension {

    public static string UntilText1(this DateTime before, DateTime after) {
      // TODO test correctly implemented
      TimeSpan diff = after - before;
      int h = (int)diff.TotalHours;
      if (h > 0) {
        return h == 1 ? $"{h} tund" : $"{h} tundi";
      } else {
        int m = (int)diff.TotalMinutes;
        if (m > 0) {
          return m == 1 ? $"{m} minut" : $"{m} minutit";
        } else {
          int s = (int)diff.TotalSeconds;
          return s == 1 ? $"{s} sekund" : $"{s} sekundit";
        }
      }
    }

    public static string UntilText2(this DateTime before, DateTime after) {
      // TODO test correctly implemented
      TimeSpan diff = after - before;
      int h = (int)diff.TotalHours;
      if (h > 0) {
        return $"{h} tunni";
      } else {
        int m = (int)diff.TotalMinutes;
        if (m > 0) {
          return $"{m} minuti";
        } else {
          int s = (int)diff.TotalSeconds;
          return $"{s} sekundi";
        }
      }
    }

  }
}
