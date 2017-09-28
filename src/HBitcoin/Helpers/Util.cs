using System;
using System.Collections.Generic;
using System.Linq;

namespace HBitcoin
{
	internal static class Util
    {
		internal static byte[][] Separate(byte[] source, byte[] separator)
		{
			var Parts = new List<byte[]>();
			var Index = 0;
			byte[] Part;
			for (var I = 0; I < source.Length; ++I)
			{
				if (Equals(source, separator, I))
				{
					Part = new byte[I - Index];
					Array.Copy(source, Index, Part, 0, Part.Length);
					Parts.Add(Part);
					Index = I + separator.Length;
					I += separator.Length - 1;
				}
			}
			Part = new byte[source.Length - Index];
			Array.Copy(source, Index, Part, 0, Part.Length);
			Parts.Add(Part);
			return Parts.ToArray();
		}

		private static bool Equals(byte[] source, byte[] separator, int index)
		{
			for (int i = 0; i < separator.Length; ++i)
				if (index + i >= source.Length || source[index + i] != separator[i])
					return false;
			return true;
		}

		/// <summary>
		/// Splits an array into several smaller arrays.
		/// </summary>
		/// <typeparam name="T">The type of the array.</typeparam>
		/// <param name="array">The array to split.</param>
		/// <param name="size">The size of the smaller arrays.</param>
		/// <returns>An array containing smaller arrays.</returns>
		public static IEnumerable<IEnumerable<T>> Split<T>(T[] array, int size)
		{
			for (var i = 0; i < (float)array.Length / size; i++)
			{
				yield return array.Skip(i * size).Take(size);
			}
		}
	}
}
