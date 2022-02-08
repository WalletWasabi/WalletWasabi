namespace Gma.QrCodeNet.Encoding;

public class TriStateMatrix : BitMatrixBase
{
	public TriStateMatrix(int width) : base(width, new bool[width, width])
	{
		StateMatrix = new StateMatrix(width);
	}

	internal TriStateMatrix(bool[,] internalArray) : base(internalArray)
	{
		StateMatrix = new StateMatrix(internalArray.GetLength(0));
	}

	private StateMatrix StateMatrix { get; }

	public override bool this[int i, int j]
	{
		get => InternalArray[i, j];
		set
		{
			if (MStatus(i, j) is MatrixStatus.None or MatrixStatus.NoMask)
			{
				throw new InvalidOperationException($"The value of cell [{i}, {j}] is not set or is Stencil.");
			}
			InternalArray[i, j] = value;
		}
	}

	public bool this[int i, int j, MatrixStatus mstatus]
	{
		set
		{
			StateMatrix[i, j] = mstatus;
			InternalArray[i, j] = value;
		}
	}

	public override int Height => Width;

	public override int Width => base.Width;

	internal MatrixStatus MStatus(int i, int j) => StateMatrix[i, j];

	internal MatrixStatus MStatus(MatrixPoint point) => MStatus(point.X, point.Y);
}
