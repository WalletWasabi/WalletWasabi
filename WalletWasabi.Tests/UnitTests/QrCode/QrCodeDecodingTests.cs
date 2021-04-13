using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using QRCodeDecoderLibrary;
using System.Drawing;
using System.IO;

namespace WalletWasabi.Tests.UnitTests.QrCode
{
	public class QrCodeDecodingTests
	{
		[Fact]
		public void GetCorrectAddressFromImage()
		{
			QRDecoder qRDecoder = new();
			string path = @"..\..\..\UnitTests\QrCode\QrTestResources\AddressTest1.png";
			using Bitmap qRCodeInputImage = new Bitmap(path);
			byte[][]? dataByteArray = qRDecoder.ImageDecoder(qRCodeInputImage);
			Assert.NotNull(dataByteArray);
			string expectedAddress = "tb1ql27ya3gufs5h0ptgjhjd0tm52fq6q0xrav7xza";
			string address = qRDecoder.QRCodeResult(dataByteArray);
			Assert.Equal(expectedAddress, address);

			string path2 = @"..\..\..\UnitTests\QrCode\QrTestResources\AddressTest2.png";
			using Bitmap otherQRCodeInputImage = new Bitmap(path2);
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

			using Bitmap qRCodeInputImage = new Bitmap(@"..\..\..\UnitTests\QrCode\QrTestResources\AddressTest1.png");
			byte[][]? dataByteArray = qRDecoder.ImageDecoder(qRCodeInputImage);
			Assert.NotNull(dataByteArray);

			using Bitmap notValidInputImage = new Bitmap(@"..\..\..\UnitTests\QrCode\QrTestResources\NotBitcoinAddress.jpg");
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
