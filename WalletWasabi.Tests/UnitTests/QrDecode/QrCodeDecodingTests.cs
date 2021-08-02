using Xunit;
using OpenCvSharp;
using System.IO;
using WalletWasabi.Helpers;
using System.Runtime.InteropServices;

namespace WalletWasabi.Tests.UnitTests.QrDecode
{
	public class QrCodeDecodingTests
	{
		private readonly string _commonPartialPath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "UnitTests", "QrDecode", "QrResources");
		private readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

		[Fact]
		public void GetCorrectAddressFromImages()
		{
			if (!_isWindows)
			{
				return;
			}
			using QRCodeDetector decoder = new();
			string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
			string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";

			string path = Path.Combine(_commonPartialPath, "AddressTest1.png");
			using var qrImage = new Mat(path);
			bool qrFound = decoder.Detect(new Mat(path), out var points);

			Assert.True(qrFound);
			string address = decoder.Decode(qrImage, points);
			Assert.Equal(expectedAddress, address);

			string otherPath = Path.Combine(_commonPartialPath, "AddressTest2.png");
			using var secondQrImage = new Mat(otherPath);
			qrFound = decoder.Detect(secondQrImage, out var otherPoints);

			Assert.True(qrFound);
			string secondAddress = decoder.Decode(qrImage, points);
			Assert.NotEqual(otherExpectedAddress, secondAddress);
		}

		[Fact]
		public void DecodePictureTakenByPhone()
		{
			if (!_isWindows)
			{
				return;
			}
			using QRCodeDetector decoder = new();
			string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

			string path = Path.Combine(_commonPartialPath, "QrByPhone.jpg");
			using var qrImage = new Mat(path);
			bool qrFound = decoder.Detect(new Mat(path), out var points);

			Assert.True(qrFound);
			string address = decoder.Decode(qrImage, points);
			Assert.Equal(expectedOutput, address);
		}

		[Fact]
		public void DecodeDifficultPictureTakenByPhone()
		{
			if (!_isWindows)
			{
				return;
			}
			using QRCodeDetector decoder = new();
			string expectedOutput = "Let's see a Zebra.";

			string path = Path.Combine(_commonPartialPath, "QRwithZebraBackground.png");
			using var qrImage = new Mat(path);
			bool qrFound = decoder.Detect(new Mat(path), out var points);

			Assert.True(qrFound);
			string address = decoder.Decode(qrImage, points);
			Assert.Equal(expectedOutput, address);
		}

		[Fact]
		public void DecodePictureWithImageInsideTheQR()
		{
			if (!_isWindows)
			{
				return;
			}
			using QRCodeDetector decoder = new();
			string expectedOutput = "https://twitter.com/SimonHearne";

			string path = Path.Combine(_commonPartialPath, "qr-embed-logos.png");
			using var qrImage = new Mat(path);
			bool qrFound = decoder.Detect(new Mat(path), out var points);

			Assert.True(qrFound);
			string address = decoder.Decode(qrImage, points);
			Assert.Equal(expectedOutput, address);
		}
	}
}
