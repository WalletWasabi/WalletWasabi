using System;

namespace Gma.QrCodeNet.Encoding
{
    public class SquareBitMatrix : BitMatrix
    {
        private readonly bool[,] m_InternalArray;

        private readonly int m_Width;

        public SquareBitMatrix(int width)
        {
            m_InternalArray = new bool[width, width];
            m_Width = width;
        }

        internal SquareBitMatrix(bool[,] internalArray)
        {
            m_InternalArray = internalArray;
            int width = internalArray.GetLength(0);
            m_Width = width;
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
                bool[,] deepCopyArray = new bool[m_Width, m_Width];
                for (int x = 0; x < m_Width; x++)
                    for (int y = 0; y < m_Width; y++)
                        deepCopyArray[x, y] = m_InternalArray[x, y];
                return deepCopyArray;
            }
        }

        

        public override bool this[int i, int j]
        {
            get
            {
                return m_InternalArray[i, j];
            }
            set
            {
                m_InternalArray[i, j] = value;
            }
        }
       
         public override int Height
        {
            get { return Width; }
        }

        public override int Width
        {
            get { return m_Width; }
        }
    }
}
