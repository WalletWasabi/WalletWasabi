namespace Gma.QrCodeNet.Encoding;

/// <summary>
/// Contain most of common constant variables. S
/// </summary>
public static class QRCodeConstantVariable
{
	public const int MinVersion = 1;
	public const int MaxVersion = 40;

	public const string DefaultEncoding = "iso-8859-1";
	public const string UTF8Encoding = "utf-8";

	/// <summary>
	/// ISO/IEC 18004:2006(E) Page 45 Chapter Generating the error correction codewords
	/// Primitive Polynomial = Bin 100011101 = Dec 285
	/// </summary>
	public const int QRCodePrimitive = 285;

	internal const int TerminatorNPaddingBit = 0;

	internal const int TerminatorLength = 4;

	/// <summary>
	/// 0xEC
	/// </summary>
	internal const int PadeCodewordsOdd = 0xec;

	/// <summary>
	/// 0x11
	/// </summary>
	internal const int PadeCodewordsEven = 0x11;

	internal const int PositionStencilWidth = 7;

	internal static bool[] PadeOdd = new bool[]
	{
			true, true, true, false,
			true, true, false, false
	};

	internal static bool[] PadeEven = new bool[]
	{
			false, false, false, true,
			false, false, false, true
	};

	/// <summary>
	/// URL:http://en.wikipedia.org/wiki/Byte-order_mark
	/// </summary>
	public static byte[] UTF8ByteOrderMark => new byte[] { 0xEF, 0xBB, 0xBF };
}
