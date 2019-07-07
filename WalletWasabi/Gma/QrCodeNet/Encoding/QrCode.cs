namespace Gma.QrCodeNet.Encoding
{
	/// <summary>
	/// This class contain two variables.
	/// BitMatrix for QRCode
	/// isContainMatrix for indicate whether QRCode contains BitMatrix or not.
	/// BitMatrix will equal to null if isContainMatrix is false.
	/// </summary>
	public class QRCode
	{
		internal QRCode(BitMatrix matrix)
		{
			Matrix = matrix;
			IsContainMatrix = true;
		}

		public QRCode()
		{
			IsContainMatrix = false;
			Matrix = null;
		}

		public bool IsContainMatrix
		{
			get;
			private set;
		}

		public BitMatrix Matrix
		{
			get;
			private set;
		}
	}
}
