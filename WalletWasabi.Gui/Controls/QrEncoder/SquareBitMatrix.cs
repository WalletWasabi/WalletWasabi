using System;

namespace Gma.QrCodeNet.Encoding
{
	public class SquareBitMatrix : BitMatrixBase
	{
		public SquareBitMatrix(int width) : base(width, new bool[width, width])
		{
		}

		internal SquareBitMatrix(bool[,] internalArray) : base(internalArray)
		{
		}

		public static bool CreateSquareBitMatrix(bool[,] internalArray, out SquareBitMatrix triStateMatrix)
		{
			triStateMatrix = null;

			if (CanCreate(internalArray))
			{
				triStateMatrix = new SquareBitMatrix(internalArray);
				return true;
			}
			return false;
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
