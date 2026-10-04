namespace WalletWasabi.Mobile;

public static class CameraFrame
{
  public static int RelativeRotation(int sensorDegrees, int displayDegrees) => ((sensorDegrees - displayDegrees) % 360 + 360) % 360;
  public static byte[] Luminance(ReadOnlySpan<byte> plane, int width, int height, int rowStride, int pixelStride)
  {
    if (width is < 1 or > 1280 || height is < 1 or > 1280 || pixelStride is < 1 or > 8
      || rowStride < checked((width - 1) * pixelStride + 1)
      || plane.Length < checked((height - 1) * rowStride + (width - 1) * pixelStride + 1))
    { throw new ArgumentException("Invalid camera frame dimensions or strides."); }
    var result = new byte[checked(width * height)];
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++) { result[y * width + x] = plane[y * rowStride + x * pixelStride]; }
    return result;
  }
}
