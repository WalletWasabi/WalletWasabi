using System.Linq;
using System.Runtime.InteropServices;

namespace System
{
	unsafe public static class ByteHelpers
	{
		// https://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
		/// <summary>
		/// Fastest byte array concatenation in C#
		/// </summary>
		public static byte[] Combine(params byte[][] arrays)
		{
			byte[] ret = new byte[arrays.Sum(x => x.Length)];
			int offset = 0;
			foreach (byte[] data in arrays)
			{
				Buffer.BlockCopy(data, 0, ret, offset, data.Length);
				offset += data.Length;
			}
			return ret;
		}

		// https://stackoverflow.com/a/8808245/2061103
		// Copyright (c) 2008-2013 Hafthor Stefansson
		// Distributed under the MIT/X11 software license
		// Ref: http://www.opensource.org/licenses/mit-license.php.
		/// <summary>
		/// Fastest byte array comparison in C#
		/// </summary>
		public static unsafe bool CompareFastUnsafe(byte[] array1, byte[] array2)
		{
			if (array1 == array2)
			{
				return true;
			}

			if (array1 == null || array2 == null || array1.Length != array2.Length)
			{
				return false;
			}

			fixed (byte* p1 = array1, p2 = array2)
			{
				byte* x1 = p1, x2 = p2;
				int l = array1.Length;
				for (int i = 0; i < l / 8; i++, x1 += 8, x2 += 8)
				{
					if (*((long*)x1) != *((long*)x2))
					{
						return false;
					}
				}

				if ((l & 4) != 0)
				{
					if (*((int*)x1) != *((int*)x2))
					{
						return false;
					}
					x1 += 4;
					x2 += 4;
				}
				if ((l & 2) != 0)
				{
					if (*((short*)x1) != *((short*)x2))
					{
						return false;
					}
					x1 += 2;
					x2 += 2;
				}
				if ((l & 1) != 0)
				{
					if (*((byte*)x1) != *((byte*)x2))
					{
						return false;
					}
				}

				return true;
			}
		}
	}
}
