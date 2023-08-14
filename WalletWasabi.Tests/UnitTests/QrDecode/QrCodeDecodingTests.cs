using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;
using WalletWasabi.Helpers;
using Xunit;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.SkiaSharp;

namespace WalletWasabi.Tests.UnitTests.QrDecode;

public class QrCodeDecodingTests
{
	private string CommonPartialPath { get; } = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "UnitTests", "QrDecode", "QrResources");
	private bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	[Fact]
	public void GetCorrectAddressFromImages()
	{
		if (!IsWindows)
		{
			return;
		}
		QRCodeReader decoder = new();

		string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
		string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";

		// First Test
		string path = Path.Combine(CommonPartialPath, "AddressTest1.png");
		var result = DecodeFromImagePath(decoder, path);

		Assert.Equal(expectedAddress, result);

		// Second Test
		string path2 = Path.Combine(CommonPartialPath, "AddressTest2.png");
		var result2 = DecodeFromImagePath(decoder, path2);

		Assert.Equal(otherExpectedAddress, result2);
	}

	[Fact]
	public void DecodePictureTakenByPhone()
	{
		if (!IsWindows)
		{
			return;
		}
		QRCodeReader decoder = new();
		string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

		string path = Path.Combine(CommonPartialPath, "QrByPhone.jpg");
		var result = DecodeFromImagePath(decoder, path);

		Assert.Equal(expectedOutput, result);
	}

	[Fact]
	public void DecodeDifficultPictureTakenByPhone()
	{
		if (!IsWindows)
		{
			return;
		}
		QRCodeReader decoder = new();
		string expectedOutput = "Let's see a Zebra.";

		string path = Path.Combine(CommonPartialPath, "QRwithZebraBackground.png");
		var result = DecodeFromImagePath(decoder, path);
		Assert.Equal(expectedOutput, result);
	}

	[Fact]
	public void DecodePictureWithImageInsideTheQR()
	{
		if (!IsWindows)
		{
			return;
		}
		QRCodeReader decoder = new();
		string expectedOutput = "https://twitter.com/SimonHearne";

		string path = Path.Combine(CommonPartialPath, "qr-embed-logos.png");
		var result = DecodeFromImagePath(decoder, path);
		Assert.Equal(expectedOutput, result);
	}

	private static string DecodeFromImagePath(QRCodeReader reader, string path)
	{
		using var bitmap = SKBitmap.Decode(path);
		var source = new SKBitmapLuminanceSource(bitmap);
		var binary = new BinaryBitmap(new HybridBinarizer(source));
		return reader.decode(binary).Text;
	}
}
