using Xunit;
using System.IO;
using WalletWasabi.Helpers;
using ZXing.QrCode;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using SkiaSharp;

namespace WalletWasabi.Tests.UnitTests.QrDecode;

public class QrCodeDecodingTests
{
	private string CommonPartialPath { get; } = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "UnitTests", "QrDecode", "QrResources");

	[Fact]
	public void GetCorrectAddressFromImages()
	{
		QRCodeReader decoder = new();

		string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
		string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";

		// First Test
		string path = Path.Combine(CommonPartialPath, "AddressTest1.png");
		var result = DecodeFromImagePath(decoder, path);

		Assert.Equal(expectedAddress, result.Text);

		// Second Test
		string path2 = Path.Combine(CommonPartialPath, "AddressTest2.png");
		var result2 = DecodeFromImagePath(decoder, path2);

		Assert.Equal(otherExpectedAddress, result2.Text);
	}

	[Fact]
	public void DecodePictureTakenByPhone()
	{
		QRCodeReader decoder = new();
		string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

		string path = Path.Combine(CommonPartialPath, "QrByPhone.jpg");
		var result = DecodeFromImagePath(decoder, path);

		Assert.Equal(expectedOutput, result.Text);
	}

	[Fact]
	public void DecodeDifficultPictureTakenByPhone()
	{
		QRCodeReader decoder = new();
		string expectedOutput = "Let's see a Zebra.";

		string path = Path.Combine(CommonPartialPath, "QRwithZebraBackground.png");
		var result = DecodeFromImagePath(decoder, path);
		Assert.Equal(expectedOutput, result.Text);
	}

	[Fact]
	public void DecodePictureWithImageInsideTheQR()
	{
		QRCodeReader decoder = new();
		string expectedOutput = "https://twitter.com/SimonHearne";

		string path = Path.Combine(CommonPartialPath, "qr-embed-logos.png");
		var result = DecodeFromImagePath(decoder, path);
		Assert.Equal(expectedOutput, result.Text);
	}

	private static Result DecodeFromImagePath(QRCodeReader reader, string path)
	{
		using var bitmap = SKBitmap.Decode(path);
			
		SKBitmapLuminanceSource source = new(bitmap);
		GlobalHistogramBinarizer binarizer = new(source);
		BinaryBitmap binaryBitmap = new(binarizer);

		return reader.decode(binaryBitmap);
	}
}
