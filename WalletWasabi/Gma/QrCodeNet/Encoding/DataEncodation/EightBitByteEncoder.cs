using System;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	/// <summary>
	/// EightBitByte is a bit complicate compare to other encoding.
	/// It can accept several different encoding table from global ECI table.
	/// For different country, default encoding is different. JP use shift_jis, International spec use iso-8859-1
	/// China use ASCII which is first part of normal char table. Between 00 to 7E
	/// Korean and Thai should have their own default encoding as well. But so far I cannot find their specification freely online.
	/// QrCode.Net will use international standard which is iso-8859-1 as default encoding.
	/// And use UTF8 as suboption for any string that not belong to any char table or other encoder.
	/// </summary>
	/// <remarks>ISO/IEC 18004:2000 Chapter 8.4.4 Page 22</remarks>
	internal class EightBitByteEncoder : EncoderBase
	{
		private const string DefaultEncoding = QRCodeConstantVariable.DefaultEncoding;

		internal string Encoding { get; private set; }

		/// <summary>
		/// EightBitByte encoder's encoding will change according to different region
		/// </summary>
		/// <param name="encoding">Default encoding is "iso-8859-1"</param>
		internal EightBitByteEncoder(string encoding) : base()
		{
			Encoding = encoding ?? DefaultEncoding;
		}

		internal EightBitByteEncoder() : base()
		{
			Encoding = DefaultEncoding;
		}

		protected byte[] EncodeContent(string content, string encoding) => System.Text.Encoding.GetEncoding(encoding).GetBytes(content);

		/// <summary>
		/// Bitcount, Chapter 8.4.4, P.24
		/// </summary>
		private const int EightBitByteBitcount = 8;

		internal override BitList GetDataBits(string content)
		{
			var eciSet = new ECISet(ECISet.AppendOption.NameToValue);
			if (!eciSet.ContainsECIName(Encoding))
			{
				throw new ArgumentOutOfRangeException(
					nameof(Encoding),
					"Current ECI table does not support this encoding. Please check ECISet class for more info");
			}

			byte[] contentBytes = EncodeContent(content, Encoding);

			return GetDataBitsByByteArray(contentBytes, Encoding);
		}

		internal BitList GetDataBitsByByteArray(byte[] encodeContent, string encodingName)
		{
			var dataBits = new BitList();
			//Current plan for UTF8 support is put Byte order Mark in front of content byte.
			//Also include ECI header before encoding header. Which will be add with encoding header.
			if (encodingName == "utf-8")
			{
				byte[] utf8BOM = QRCodeConstantVariable.UTF8ByteOrderMark;
				for (int index = 0; index < utf8BOM.Length; index++)
				{
					dataBits.Add(utf8BOM[index], EightBitByteBitcount);
				}
			}

			for (int index = 0; index < encodeContent.Length; index++)
			{
				dataBits.Add(encodeContent[index], EightBitByteBitcount);
			}
			return dataBits;
		}

		protected override int GetBitCountInCharCountIndicator(int version) => CharCountIndicatorTable.GetBitCountInCharCountIndicator(version);
	}
}
