namespace Gma.QrCodeNet.Encoding;

public abstract class BitMatrixBase : BitMatrix
{
	protected BitMatrixBase(int width, bool[,] internalArray)
	{
		Width = width;
		InternalArray = internalArray;
	}

	protected BitMatrixBase(bool[,] internalArray)
	{
		InternalArray = internalArray;
		int width = internalArray.GetLength(0);
		Width = width;
	}

	public override bool[,] InternalArray { get; }

	public override int Width { get; }

	public static bool CanCreate(bool[,] internalArray)
	{
		if (internalArray is null)
		{
			return false;
		}

		return internalArray.GetLength(0) == internalArray.GetLength(1);
	}
}
