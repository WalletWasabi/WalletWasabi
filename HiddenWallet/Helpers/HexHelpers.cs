using System.Collections.Generic;
using System.Text;

namespace System
{
    public static class HexHelpers
    {
		public static string ToString(byte[] ba)
		{
			var hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}

		public static byte[] GetBytes(string hex)
		{
			int NumberChars = hex.Length;
			var bytes = new byte[NumberChars / 2];
			for (int i = 0; i < NumberChars; i += 2)
				bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			return bytes;
		}
	}
}
