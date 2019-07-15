namespace Gma.QrCodeNet.Encoding
{
	public abstract class BitMatrixBase : BitMatrix
	{
		public override bool[,] InternalArray { get; }

		public override int Width { get; }

		protected BitMatrixBase(int width, bool[,] internalArray)
		{
			Width = width;
			InternalArray = internalArray;
		}

		protected BitMatrixBase(bool[,] internalArray)
		{
			InternalArray = internalArray;
			int width = internalArray.GetLength(0);
			Width = width;
		}

		public static bool CanCreate(bool[,] internalArray)
		{
			if (internalArray is null)
			{
				return false;
			}

			return internalArray.GetLength(0) == internalArray.GetLength(1);
		}

		/// <summary>
		/// Return value will be deep copy of array.
		/// </summary>
		public bool[,] CloneInternalArray()
		{
			bool[,] deepCopyArray = new bool[Width, Width];
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Width; y++)
				{
					deepCopyArray[x, y] = InternalArray[x, y];
				}
			}

			return deepCopyArray;
		}
	}
}
