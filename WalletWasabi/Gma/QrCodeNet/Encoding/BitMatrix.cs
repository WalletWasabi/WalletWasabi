namespace Gma.QrCodeNet.Encoding;

public abstract class BitMatrix
{
	public abstract int Width { get; }
	public abstract int Height { get; }
	public abstract bool[,] InternalArray { get; }

	public abstract bool this[int i, int j] { get; set; }

	internal void CopyTo(TriStateMatrix target, MatrixRectangle sourceArea, MatrixPoint targetPoint, MatrixStatus mstatus)
	{
		for (int j = 0; j < sourceArea.Size.Height; j++)
		{
			for (int i = 0; i < sourceArea.Size.Width; i++)
			{
				bool value = this[sourceArea.Location.X + i, sourceArea.Location.Y + j];
				target[targetPoint.X + i, targetPoint.Y + j, mstatus] = value;
			}
		}
	}

	internal void CopyTo(TriStateMatrix target, MatrixPoint targetPoint, MatrixStatus mstatus) => CopyTo(target, new MatrixRectangle(new MatrixPoint(0, 0), new MatrixSize(Width, Height)), targetPoint, mstatus);
}
