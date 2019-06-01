using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding
{
	public class QrEncoder
	{
		public ErrorCorrectionLevel ErrorCorrectionLevel { get; set; }

		/// <summary>
		/// Default QrEncoder will set ErrorCorrectionLevel as M
		/// </summary>
		public QrEncoder()
			: this(ErrorCorrectionLevel.M)
		{
		}

		/// <summary>
		/// QrEncoder with parameter ErrorCorrectionLevel.
		/// </summary>
		/// <param name="errorCorrectionLevel"></param>
		public QrEncoder(ErrorCorrectionLevel errorCorrectionLevel)
		{
			ErrorCorrectionLevel = errorCorrectionLevel;
		}

		/// <summary>
		/// Encode string content to QrCode matrix
		/// </summary>
		/// <exception cref="InputOutOfBoundaryException">
		/// This exception for string content is null, empty or too large</exception>
		public QrCode Encode(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				throw new InputOutOfBoundaryException("Input should not be empty or null");
			}
			else
			{
				return new QrCode(QRCodeEncode.Encode(content, ErrorCorrectionLevel));
			}
		}

		/// <summary>
		/// Try to encode content
		/// </summary>
		/// <returns>False if input content is empty, null or too large.</returns>
		public bool TryEncode(string content, out QrCode qrCode)
		{
			try
			{
				qrCode = Encode(content);
				return true;
			}
			catch (InputOutOfBoundaryException)
			{
				qrCode = new QrCode();
				return false;
			}
		}

		/// <summary>
		/// Encode byte array content to QrCode matrix
		/// </summary>
		/// <exception cref="InputOutOfBoundaryException">
		/// This exception for string content is null, empty or too large</exception>
		public QrCode Encode(IEnumerable<byte> content)
		{
			if (content is null)
			{
				throw new InputOutOfBoundaryException("Input should not be empty or null");
			}
			else
			{
				return new QrCode(QRCodeEncode.Encode(content, ErrorCorrectionLevel));
			}
		}

		/// <summary>
		/// Try to encode content
		/// </summary>
		/// <returns>False if input content is empty, null or too large.</returns>
		public bool TryEncode(IEnumerable<byte> content, out QrCode qrCode)
		{
			try
			{
				qrCode = Encode(content);
				return true;
			}
			catch (InputOutOfBoundaryException)
			{
				qrCode = new QrCode();
				return false;
			}
		}
	}
}
