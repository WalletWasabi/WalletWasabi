namespace Gma.QrCodeNet.Encoding;

/// <summary>
/// This class contain two variables.
/// BitMatrix for QrCode
/// isContainMatrix for indicate whether QrCode contains BitMatrix or not.
/// BitMatrix will be equal to null if isContainMatrix is false.
/// </summary>
public class QrCode
{
	internal QrCode(BitMatrix matrix)
	{
		Matrix = matrix;
		IsContainMatrix = true;
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
