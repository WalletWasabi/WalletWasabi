namespace Gma.QrCodeNet.Encoding
{
	public struct MatrixPoint
	{
		public int X { get; private set; }
		public int Y { get; private set; }

		internal MatrixPoint(int x, int y)
			: this()
		{
			X = x;
			Y = y;
		}

		public MatrixPoint Offset(MatrixPoint offset) => new MatrixPoint(offset.X + X, offset.Y + Y);

		internal MatrixPoint Offset(int offsetX, int offsetY) => Offset(new MatrixPoint(offsetX, offsetY));

		public override string ToString() => $"Point({X};{Y})";
	}
}
