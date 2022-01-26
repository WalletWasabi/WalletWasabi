using Xunit;
using OpenCvSharp;
using System.IO;
using WalletWasabi.Helpers;
using System.Runtime.InteropServices;

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
		using QRCodeDetector decoder = new();
		string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
		string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";

		// First Test
		string path = Path.Combine(CommonPartialPath, "AddressTest1.png");
		using var qrImage = new Mat(path);
		bool qrFound = decoder.Detect(qrImage, out var points);
		Assert.True(qrFound);

		string address = decoder.Decode(qrImage, points);
		Assert.Equal(expectedAddress, address);

		// Second Test
		string otherPath = Path.Combine(CommonPartialPath, "AddressTest2.png");
		using var secondQrImage = new Mat(otherPath);
		qrFound = decoder.Detect(secondQrImage, out var otherPoints);
		Assert.True(qrFound);

		string secondAddress = decoder.Decode(secondQrImage, otherPoints);
		Assert.Equal(otherExpectedAddress, secondAddress);
	}

	[Fact]
	public void DecodePictureTakenByPhone()
	{
		if (!IsWindows)
		{
			return;
		}
		using QRCodeDetector decoder = new();
		string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

		string path = Path.Combine(CommonPartialPath, "QrByPhone.jpg");
		using var qrImage = new Mat(path);
		bool qrFound = decoder.Detect(qrImage, out var points);
		Assert.True(qrFound);

		string address = decoder.Decode(qrImage, points);
		Assert.Equal(expectedOutput, address);
	}

	[Fact]
	public void DecodeDifficultPictureTakenByPhone()
	{
		if (!IsWindows)
		{
			return;
		}
		using QRCodeDetector decoder = new();
		string expectedOutput = "Let's see a Zebra.";

		string path = Path.Combine(CommonPartialPath, "QRwithZebraBackground.png");
		using var qrImage = new Mat(path);
		bool qrFound = decoder.Detect(qrImage, out var points);
		Assert.True(qrFound);

		string address = decoder.Decode(qrImage, points);
		Assert.Equal(expectedOutput, address);
	}

	[Fact]
	public void DecodePictureWithImageInsideTheQR()
	{
		if (!IsWindows)
		{
			return;
		}
		using QRCodeDetector decoder = new();
		string expectedOutput = "https://twitter.com/SimonHearne";

		string path = Path.Combine(CommonPartialPath, "qr-embed-logos.png");
		using var qrImage = new Mat(path);
		bool qrFound = decoder.Detect(qrImage, out var points);
		Assert.True(qrFound);

		string address = decoder.Decode(qrImage, points);
		Assert.Equal(expectedOutput, address);
	}
}
