using System.Diagnostics;

namespace IsikReg.Utils {
  public static class Measure {

    private static Stopwatch? watch;

    public static void Start() {
      watch = Stopwatch.StartNew();
    }

    public static long ElapsedMillis() {
      return watch?.ElapsedMilliseconds ?? 0;
    }

    public static void Stop() {
      watch?.Stop();
    }

  }
}
