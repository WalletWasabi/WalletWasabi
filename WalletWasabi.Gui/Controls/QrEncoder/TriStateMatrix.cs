using System;

namespace Gma.QrCodeNet.Encoding
{
	public class TriStateMatrix : BitMatrix
	{
		private readonly StateMatrix M_stateMatrix;

		private readonly bool[,] M_InternalArray;

		private readonly int M_Width;

		public TriStateMatrix(int width)
		{
			M_stateMatrix = new StateMatrix(width);
			M_InternalArray = new bool[width, width];
			M_Width = width;
		}

		public static bool CreateTriStateMatrix(bool[,] internalArray, out TriStateMatrix triStateMatrix)
		{
			triStateMatrix = null;
			if (internalArray == null)
				return false;

			if (internalArray.GetLength(0) == internalArray.GetLength(1))
			{
				triStateMatrix = new TriStateMatrix(internalArray);
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Return value will be deep copy of array.
		/// </summary>
		public override bool[,] InternalArray
		{
			get
			{
				bool[,] deepCopyArray = new bool[M_Width, M_Width];
				for (int x = 0; x < M_Width; x++)
					for (int y = 0; y < M_Width; y++)
						deepCopyArray[x, y] = M_InternalArray[x, y];
				return deepCopyArray;
			}
		}

		internal TriStateMatrix(bool[,] internalArray)
		{
			M_InternalArray = internalArray;
			int width = internalArray.GetLength(0);
			M_stateMatrix = new StateMatrix(width);
			M_Width = width;
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
