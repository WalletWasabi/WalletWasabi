namespace Gma.QrCodeNet.Encoding
{
	public sealed class StateMatrix
	{
		private readonly MatrixStatus[,] _m_matrixStatus;

		public StateMatrix(int width)
		{
			Width = width;
			_m_matrixStatus = new MatrixStatus[width, width];
		}

		public MatrixStatus this[int x, int y]
		{
			get => _m_matrixStatus[x, y];
			set => _m_matrixStatus[x, y] = value;
		}

		internal MatrixStatus this[MatrixPoint point]
		{
			get
			{ return this[point.X, point.Y]; }
			set
			{ this[point.X, point.Y] = value; }
		}

		public int Width { get; }

		public int Height => Width;
	}
}
