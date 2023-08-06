namespace IsikReg.Extensions {
  public static class ByteExtension {
    public static byte Lerp(this byte source, byte target, double percent) {
      return (byte)(source * (1 - percent) + target * percent);
    }
  }
}
