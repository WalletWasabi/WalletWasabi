using System;

namespace Gma.QrCodeNet.Encoding.Common
{
    public sealed class ByteMatrix
    {
        private readonly sbyte[,] m_Bytes;
        
        internal sbyte this[int x, int y]
        {
            get { return m_Bytes[y, x]; }
            set { m_Bytes[y, x] = value; }
        }
        
        internal int Width
        {
            get { return m_Bytes.GetLength(1); }
        }

        internal int Height
        {
            get { return m_Bytes.GetLength(0); }
        }

        internal ByteMatrix(int width, int height)
        {
            m_Bytes = new sbyte[height, width];
        }

        internal void Clear(sbyte value)
        {
            this.ForAll((x, y, matrix) => { matrix[x, y] = value; });
        }

        internal void ForAll(Action<int, int, ByteMatrix> action)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    action.Invoke(x, y, this);
                }
            }
        }
    }
}