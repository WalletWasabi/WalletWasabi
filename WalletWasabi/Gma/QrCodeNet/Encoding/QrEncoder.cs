using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding
{
	public class QrEncoder
	{
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
		public QrEncoder(ErrorCorrectionLevel errorCorrectionLevel)
		{
			ErrorCorrectionLevel = errorCorrectionLevel;
		}

		public ErrorCorrectionLevel ErrorCorrectionLevel { get; set; }

		/// <summary>
		/// Encode string content to QrCode matrix
		/// </summary>
		/// <exception cref="InputOutOfBoundaryException">
		/// This exception for string content is null, empty or too large</exception>
		public QrCode Encode(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				throw new InputOutOfBoundaryException("Input cannot be null or empty.");
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
	}
}
