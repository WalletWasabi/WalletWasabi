using System;

namespace Gma.QrCodeNet.Encoding
{
	public class TriStateMatrix : BitMatrixBase
	{
		private StateMatrix StateMatrix { get; }

		public TriStateMatrix(int width) : base(width, new bool[width, width])
		{
			StateMatrix = new StateMatrix(width);
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
			StateMatrix = new StateMatrix(internalArray.GetLength(0));
		}

		public override bool this[int i, int j]
		{
			get => InternalArray[i, j];
			set
			{
				if (MStatus(i, j) == MatrixStatus.None || MStatus(i, j) == MatrixStatus.NoMask)
				{
					throw new InvalidOperationException($"The value of cell [{i},{j}] is not set or is Stencil.");
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

		internal MatrixStatus MStatus(int i, int j)
		{
			return StateMatrix[i, j];
		}

		internal MatrixStatus MStatus(MatrixPoint point)
		{
			return MStatus(point.X, point.Y);
		}

		public override int Height => Width;

		public override int Width => base.Width;
	}
}
