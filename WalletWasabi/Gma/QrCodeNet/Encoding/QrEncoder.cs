using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding
{
	public class QREncoder
	{
		public ErrorCorrectionLevel ErrorCorrectionLevel { get; set; }

		/// <summary>
		/// Default QREncoder will set ErrorCorrectionLevel as M
		/// </summary>
		public QREncoder()
			: this(ErrorCorrectionLevel.M)
		{
		}

		/// <summary>
		/// QREncoder with parameter ErrorCorrectionLevel.
		/// </summary>
		/// <param name="errorCorrectionLevel"></param>
		public QREncoder(ErrorCorrectionLevel errorCorrectionLevel)
		{
			ErrorCorrectionLevel = errorCorrectionLevel;
		}

		/// <summary>
		/// Encode string content to QRCode matrix
		/// </summary>
		/// <exception cref="InputOutOfBoundaryException">
		/// This exception for string content is null, empty or too large</exception>
		public QRCode Encode(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				throw new InputOutOfBoundaryException("Input should not be empty or null");
			}
			else
			{
				return new QRCode(QRCodeEncode.Encode(content, ErrorCorrectionLevel));
			}
		}

		/// <summary>
		/// Try to encode content
		/// </summary>
		/// <returns>False if input content is empty, null or too large.</returns>
		public bool TryEncode(string content, out QRCode qrCode)
		{
			try
			{
				qrCode = Encode(content);
				return true;
			}
			catch (InputOutOfBoundaryException)
			{
				qrCode = new QRCode();
				return false;
			}
		}

		/// <summary>
		/// Encode byte array content to QRCode matrix
		/// </summary>
		/// <exception cref="InputOutOfBoundaryException">
		/// This exception for string content is null, empty or too large</exception>
		public QRCode Encode(IEnumerable<byte> content)
		{
			if (content is null)
			{
				throw new InputOutOfBoundaryException("Input should not be empty or null");
			}
			else
			{
				return new QRCode(QRCodeEncode.Encode(content, ErrorCorrectionLevel));
			}
		}

		/// <summary>
		/// Try to encode content
		/// </summary>
		/// <returns>False if input content is empty, null or too large.</returns>
		public bool TryEncode(IEnumerable<byte> content, out QRCode qrCode)
		{
			try
			{
				qrCode = Encode(content);
				return true;
			}
			catch (InputOutOfBoundaryException)
			{
				qrCode = new QRCode();
				return false;
			}
		}
	}
}
