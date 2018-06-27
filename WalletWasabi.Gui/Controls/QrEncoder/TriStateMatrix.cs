using System;

namespace Gma.QrCodeNet.Encoding
{
	public class TriStateMatrix : BitMatrixBase
	{
		private readonly StateMatrix M_stateMatrix;

		public TriStateMatrix(int width) : base(width, new bool[width, width])
		{
			M_stateMatrix = new StateMatrix(width);
		}

		public static bool CreateTriStateMatrix(bool[,] internalArray, out TriStateMatrix triStateMatrix)
		{
			triStateMatrix = null;
			if (CanCreate(internalArray))
			{
				triStateMatrix = new TriStateMatrix(internalArray);
				return true;
			}

			return false;
		}
		internal TriStateMatrix(bool[,] internalArray) : base(internalArray)
		{
			M_stateMatrix = new StateMatrix(internalArray.GetLength(0));
		}

		public override bool this[int i, int j]
		{
			get
			{
				return M_InternalArray[i, j];
			}
			set
			{
				if (MStatus(i, j) == MatrixStatus.None || MStatus(i, j) == MatrixStatus.NoMask)
				{
					throw new InvalidOperationException(string.Format("The value of cell [{0},{1}] is not set or is Stencil.", i, j));
				}
				M_InternalArray[i, j] = value;
			}
		}

		public bool this[int i, int j, MatrixStatus mstatus]
		{
			set
			{
				M_stateMatrix[i, j] = mstatus;
				M_InternalArray[i, j] = value;
			}
		}

		internal MatrixStatus MStatus(int i, int j)
		{
			return M_stateMatrix[i, j];
		}

		internal MatrixStatus MStatus(MatrixPoint point)
		{
			return MStatus(point.X, point.Y);
		}

		public override int Height
		{
			get { return Width; }
		}

		public override int Width
		{
			get { return M_Width; }
		}
	}
}
