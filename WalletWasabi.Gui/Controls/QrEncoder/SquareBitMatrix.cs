using System;

namespace Gma.QrCodeNet.Encoding
{
	public class SquareBitMatrix : BitMatrix
	{
		private readonly bool[,] M_InternalArray;

		private readonly int M_Width;

		public SquareBitMatrix(int width)
		{
			M_InternalArray = new bool[width, width];
			M_Width = width;
		}

		internal SquareBitMatrix(bool[,] internalArray)
		{
			M_InternalArray = internalArray;
			int width = internalArray.GetLength(0);
			M_Width = width;
		}

		public static bool CreateSquareBitMatrix(bool[,] internalArray, out SquareBitMatrix triStateMatrix)
		{
			triStateMatrix = null;
			if (internalArray == null)
				return false;

			if (internalArray.GetLength(0) == internalArray.GetLength(1))
			{
				triStateMatrix = new SquareBitMatrix(internalArray);
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Return value will be internal array itself. Not deep/shallow copy.
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

		public override bool this[int i, int j]
		{
			get
			{
				return M_InternalArray[i, j];
			}
			set
			{
				M_InternalArray[i, j] = value;
			}
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
