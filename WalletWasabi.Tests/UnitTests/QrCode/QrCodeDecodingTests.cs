using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using QRCodeDecoderLibrary;
using System.Drawing;
using System.IO;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.UnitTests.QrCode
{
	public class QrCodeDecodingTests
	{
		private readonly string _commonPartialPath = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "UnitTests", "QrCode", "QrTestResources");

		[Fact]
		public void GetCorrectAddressFromImage()
		{
			QRDecoder decoder = new();

			string path = Path.Combine(_commonPartialPath, "AddressTest1.png");
			using Bitmap qRCodeInputImage = new(path);
			var dataByteArray = decoder.SearchQrCodes(qRCodeInputImage);
			Assert.NotEmpty(dataByteArray);
			string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
			string address = dataByteArray.First();
			Assert.Equal(expectedAddress, address);

			string otherPath = Path.Combine(_commonPartialPath, "AddressTest2.png");
			using Bitmap otherQRCodeInputImage = new(otherPath);
			var otherDataByteArray = decoder.SearchQrCodes(otherQRCodeInputImage);
			Assert.NotEmpty(otherDataByteArray);
			string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";
			string address2 = otherDataByteArray.First();
			Assert.Equal(otherExpectedAddress, address2);

			Assert.NotEqual(dataByteArray, otherDataByteArray);
			Assert.NotEqual(expectedAddress, address2);
		}

		[Fact]
		public void IncorrectImageReturnsEmpty()
		{
			QRDecoder decoder = new();

			string path = Path.Combine(_commonPartialPath, "AddressTest1.png");
			using Bitmap qRCodeInputImage = new(path);
			var dataByteArray = decoder.SearchQrCodes(qRCodeInputImage);
			Assert.Single(dataByteArray);

			string otherPath = Path.Combine(_commonPartialPath, "NotBitcoinAddress.jpg");
			using Bitmap notValidInputImage = new(otherPath);
			var notValidDataByteArray = decoder.SearchQrCodes(notValidInputImage);
			Assert.Empty(notValidDataByteArray);
		}

		[Fact]
		public void GetAddressFromPictureTakenByPhone()
		{
			QRDecoder decoder = new();
			string expectedOutput = "tb1qutgpgraaze3hqnvt2xyw5acsmd3urprk3ff27d";

			string path = Path.Combine(_commonPartialPath, "QrByPhone.jpg");
			using Bitmap inputImage = new(path);
			var dataCollection = decoder.SearchQrCodes(inputImage);

			Assert.Single(dataCollection);
			Assert.Equal(expectedOutput, dataCollection.First());
		}
	}
}
