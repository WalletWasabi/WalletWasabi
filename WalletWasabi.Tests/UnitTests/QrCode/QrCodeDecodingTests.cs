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
			QRDecoder qRDecoder = new();

			string path = Path.Combine(_commonPartialPath, "AddressTest1.png");
			using Bitmap qRCodeInputImage = new Bitmap(path);
			byte[][]? dataByteArray = qRDecoder.ImageDecoder(qRCodeInputImage);
			Assert.NotNull(dataByteArray);
			string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
			string address = qRDecoder.QRCodeResult(dataByteArray);
			Assert.Equal(expectedAddress, address);

			string otherPath = Path.Combine(_commonPartialPath, "AddressTest2.png");
			using Bitmap otherQRCodeInputImage = new Bitmap(otherPath);
			byte[][]? otherDataByteArray = qRDecoder.ImageDecoder(otherQRCodeInputImage);
			Assert.NotNull(otherDataByteArray);
			string otherExpectedAddress = "tb1qfas0k9rn8daqggu7wzp2yne9qdd5fr5wf2u478";
			string address2 = qRDecoder.QRCodeResult(otherDataByteArray);
			Assert.Equal(otherExpectedAddress, address2);

			Assert.NotEqual(dataByteArray, otherDataByteArray);
			Assert.NotEqual(expectedAddress, address2);
		}

		[Fact]
		public void IncorrectImageReturnsNull()
		{
			QRDecoder qRDecoder = new();

			string path = Path.Combine(_commonPartialPath, "AddressTest1.png");
			using Bitmap qRCodeInputImage = new Bitmap(path);
			byte[][]? dataByteArray = qRDecoder.ImageDecoder(qRCodeInputImage);
			Assert.NotNull(dataByteArray);

			string otherPath = Path.Combine(_commonPartialPath, "NotBitcoinAddress.jpg");
			using Bitmap notValidInputImage = new Bitmap(otherPath);
			byte[][]? notValidDataByteArray = qRDecoder.ImageDecoder(notValidInputImage);
			Assert.Null(notValidDataByteArray);
		}

		[Fact]
		public void ByteArrayToCorrectString()
		{
			string expectedString = "Expected String";
			byte[] bytes = Encoding.UTF8.GetBytes(expectedString);
			string output = QRDecoder.ByteArrayToStr(bytes);
			Assert.Equal(expectedString, output);
		}
	}
}
