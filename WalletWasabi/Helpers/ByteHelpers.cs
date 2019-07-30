using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace System
{
	public static unsafe class ByteHelpers
	{
		// https://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
		/// <summary>
		/// Fastest byte array concatenation in C#
		/// </summary>
		public static byte[] Combine(params byte[][] arrays)
		{
			return Combine(arrays.AsEnumerable());
		}

		public static byte[] Combine(IEnumerable<byte[]> arrays)
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

			if (array1 is null || array2 is null || array1.Length != array2.Length)
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
					if (*x1 != *x2)
					{
						return false;
					}
				}

				return true;
			}
		}

		private static readonly uint[] Lookup32Unsafe = CreateLookup32Unsafe();
		private static readonly uint* Lookup32UnsafeP = (uint*)GCHandle.Alloc(Lookup32Unsafe, GCHandleType.Pinned).AddrOfPinnedObject();

		private static uint[] CreateLookup32Unsafe()
		{
			var result = new uint[256];
			for (int i = 0; i < 256; i++)
			{
				string s = i.ToString("X2");
				result[i] = BitConverter.IsLittleEndian ? s[0] + ((uint)s[1] << 16) : s[1] + ((uint)s[0] << 16);
			}
			return result;
		}

		// https://stackoverflow.com/a/24343727/2061103
		/// <summary>
		/// Fastest byte array to hex implementation in C#
		/// </summary>
		public static string ToHex(params byte[] bytes)
		{
			if (bytes is null)
			{
				return null;
			}

			if (bytes.Length == 0)
			{
				return "";
			}

			var lookupP = Lookup32UnsafeP;
			var result = new string((char)0, bytes.Length * 2);
			fixed (byte* bytesP = bytes)
			fixed (char* resultP = result)
			{
				uint* resultP2 = (uint*)resultP;
				for (int i = 0; i < bytes.Length; i++)
				{
					resultP2[i] = lookupP[bytesP[i]];
				}
			}
			return result;
		}

		// https://stackoverflow.com/a/5919521/2061103
		// https://stackoverflow.com/a/10048895/2061103
		/// <summary>
		/// Fastest hex to byte array implementation in C#
		/// </summary>
		public static byte[] FromHex(string hex)
		{
			if (hex is null)
			{
				return null;
			}

			if (string.IsNullOrWhiteSpace(hex))
			{
				return new byte[0];
			}

			var bytes = new byte[hex.Length / 2];
			var hexValue = new int[]
			{
				0x00, 0x01, 0x02, 0x03, 0x04, 0x05,
				0x06, 0x07, 0x08, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F
			};

			for (int x = 0, i = 0; i < hex.Length; i += 2, x += 1)
			{
				bytes[x] = (byte)(hexValue[char.ToUpper(hex[i + 0]) - '0'] << 4 |
								  hexValue[char.ToUpper(hex[i + 1]) - '0']);
			}

			return bytes;
		}
	}
}
