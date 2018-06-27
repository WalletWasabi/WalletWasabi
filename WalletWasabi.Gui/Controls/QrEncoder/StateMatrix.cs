using System;

namespace Gma.QrCodeNet.Encoding
{
	public sealed class StateMatrix
	{
		private MatrixStatus[,] m_matrixStatus;
		private readonly int m_Width;
		
		public StateMatrix(int width)
		{
			m_Width = width;
			m_matrixStatus = new MatrixStatus[width, width];
		}
		
		public MatrixStatus this[int x, int y]
		{
			get
			{
				return m_matrixStatus[x, y];
			}
			set
			{
				m_matrixStatus[x, y] = value;
			}
		}
		
		internal MatrixStatus this[MatrixPoint point]
		{
			get
			{ return this[point.X, point.Y]; }
			set
			{ this[point.X, point.Y] = value; }
		}
		
		public int Width
		{
			get { return m_Width; }
		}
		
		public int Height
		{
			get { return this.Width; }
		}
	}
}
