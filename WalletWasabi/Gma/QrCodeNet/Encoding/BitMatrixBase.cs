namespace Gma.QrCodeNet.Encoding
{
	public abstract class BitMatrixBase : BitMatrix
	{
		public readonly bool[,] M_InternalArray;

		public readonly int M_Width;

		protected BitMatrixBase(int width, bool[,] internalArray)
		{
			M_Width = width;
			M_InternalArray = internalArray;
		}

		protected BitMatrixBase(bool[,] internalArray)
		{
			M_InternalArray = internalArray;
			int width = internalArray.GetLength(0);
			M_Width = width;
		}

		public static bool CanCreate(bool[,] internalArray)
		{
			if (internalArray is null)
				return false;

			return internalArray.GetLength(0) == internalArray.GetLength(1) ? true : false;
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
	}
}
