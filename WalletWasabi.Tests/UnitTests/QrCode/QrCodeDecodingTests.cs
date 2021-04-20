using System;
using System.Linq;
using Xunit;
using WalletWasabi.Fluent.QRCodeDecoder;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.UnitTests.QrCode
{
	public class QrCodeDecodingTests
	{
		private readonly string _commonPartialPath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "UnitTests", "QrCode", "QrTestResources");

		[Fact]
		public void GetCorrectAddressFromImage()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
			string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";

			string path = Path.Combine(_commonPartialPath, "AddressTest1.png");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.NotEmpty(dataCollection);
			Assert.Equal(expectedAddress, dataCollection.First());

			string otherPath = Path.Combine(_commonPartialPath, "AddressTest2.png");
			using var otherQRCodeInputImage = LoadBitmap(otherPath);
			var dataCollection2 = decoder.SearchQrCodes(otherQRCodeInputImage);

			Assert.NotEmpty(dataCollection2);
			Assert.Equal(otherExpectedAddress, dataCollection2.First());

			Assert.NotEqual(dataCollection, dataCollection2);
		}

		[Fact]
		public void IncorrectImageReturnsEmpty()
		{
			using var app = Start();
			QRDecoder decoder = new();

			string path = Path.Combine(_commonPartialPath, "NotBitcoinAddress.jpg");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Empty(dataCollection);
		}

		[Fact]
		public void DecodePictureTakenByPhone()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

			string path = Path.Combine(_commonPartialPath, "QrByPhone.jpg");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Single(dataCollection);
			Assert.Equal(expectedOutput, dataCollection.First());
		}

		[Fact]
		public void DecodeDifficultPictureTakenByPhone()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedOutput = "Top right corner";

			string path = Path.Combine(_commonPartialPath, "QrBrick.jpg");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Single(dataCollection);
			Assert.Equal(expectedOutput, dataCollection.First());
		}

		[Fact]
		public void DecodePictureWithZebraBackground()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedOutput = "Let's see a Zebra.";

			string path = Path.Combine(_commonPartialPath, "QRwithZebraBackground.png");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Single(dataCollection);
			Assert.Equal(expectedOutput, dataCollection.First());
		}

		[Fact]
		public void DecodePictureWithMouseOverTheQRCodeReturnsNull()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

			string path = Path.Combine(_commonPartialPath, "mouseOverQR.jpg");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Single(dataCollection);
			Assert.Equal(expectedOutput, dataCollection.First());
		}

		[Fact]
		public void DecodePictureWithImageInsideTheQR()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedOutput = "bitcoin:bc1q3r0ayktdsh8yd3krk5zkvpc5weeqnmw8ztzsgx?amount=1000&label=GIMME%201000BTC";

			string path = Path.Combine(_commonPartialPath, "Payment_details_included.jpg");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Equal(expectedOutput, dataCollection.First());
		}

		[Fact]
		public void DecodePictureWithLegacyAddress()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string expectedOutput = "bitcoin:1EYTGtG4LnFfiMvjJdsU7GMGCQvsRSjYhx";

			string path = Path.Combine(_commonPartialPath, "Random_address_starting_with_1.png");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp);

			Assert.Equal(expectedOutput, dataCollection.First());
		}

		[Fact]
		public void DecodeQrCodeInsideQrCode()
		{
			using var app = Start();
			QRDecoder decoder = new();
			string firstExpectedOutput = "Big QR Code";
			string secondExpectedOutput = "Small QR Code";
			int expectedLength = 2;

			// If there are two QR Codes on the picture, it will find both, even if they are
			// stacked on eachother, until the QR Code's quality is okay and is still readable
			string path = Path.Combine(_commonPartialPath, "DoubleQRCode.png");
			using var bmp = LoadBitmap(path);
			var dataCollection = decoder.SearchQrCodes(bmp).ToArray();

			Assert.Equal(expectedLength, dataCollection.Length);
			Assert.Equal(firstExpectedOutput, dataCollection[0]);
			Assert.Equal(secondExpectedOutput, dataCollection[1]);
		}

		private static WriteableBitmap LoadBitmap(string path)
		{
			using var fs = File.OpenRead(path);
			return WriteableBitmap.Decode(fs);
		}

		private static IDisposable Start()
		{
			var scope = AvaloniaLocator.EnterScope();
			SkiaPlatform.Initialize();
			return scope;
		}
	}
}
