using System.Windows.Media;

namespace IsikReg.Extensions {
  public static class ColorExtension {

    public static Color Derive(this Color color, double percent) {
      if (percent > 0) {
        return Lighten(color, percent);
      } else {
        return Darken(color, -percent);
      }
    }

    public static Color Lighten(this Color color, double percent) {
      return Blend(color, Colors.White, percent);
    }

    public static Color Darken(this Color color, double percent) {
      return Blend(color, Colors.Black, percent);
    }

    public static Color Blend(this Color sourceColor, Color targetColor, double percent) {
      percent = percent.Bound(0, 1);
      return Color.FromArgb(
        sourceColor.A.Lerp(targetColor.A, percent),
        sourceColor.R.Lerp(targetColor.R, percent),
        sourceColor.G.Lerp(targetColor.G, percent),
        sourceColor.B.Lerp(targetColor.B, percent)
       );
    }

  }
}
