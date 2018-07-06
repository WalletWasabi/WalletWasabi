using System;

namespace Gma.QrCodeNet.Encoding.ReedSolomon
{
	/// <summary>
	/// Description of GaloisField256.
	/// </summary>
	internal sealed class GaloisField256
	{
		private readonly int[] _antiLogTable;
		private readonly int[] _logTable;

		internal int Primitive { get; }

		internal GaloisField256(int primitive)
		{
			_antiLogTable = new int[256];
			_logTable = new int[256];

			Primitive = primitive;

			int gfx = 1;
			//Power cycle is from 0 to 254. 2^255 = 1 = 2^0
			//Value cycle is from 1 to 255. Thus there should not have Log(0).
			for (int powers = 0; powers < 256; powers++)
			{
				_antiLogTable[powers] = gfx;
				if (powers != 255)
					_logTable[gfx] = powers;
				gfx <<= 1;      //gfx = gfx * 2 where alpha is 2.

				if (gfx > 255)
				{
					gfx ^= primitive;
				}
			}
		}

		internal static GaloisField256 QRCodeGaloisField => new GaloisField256(QRCodeConstantVariable.QRCodePrimitive);

		/// <returns>
		/// Powers of a in GF table. Where a = 2
		/// </returns>
		internal int Exponent(int PowersOfa) => _antiLogTable[PowersOfa];

		/// <returns>
		/// log ( power of a) in GF table. Where a = 2
		/// </returns>
		internal int Log(int gfValue)
		{
			if (gfValue == 0)
				throw new ArgumentException("GaloisField value will not equal to 0, Log method");
			return _logTable[gfValue];
		}

		internal int Inverse(int gfValue)
		{
			if (gfValue == 0)
				throw new ArgumentException("GaloisField value will not equal to 0, Inverse method");
			return Exponent(255 - Log(gfValue));
		}

		internal int Addition(int gfValueA, int gfValueB) => gfValueA ^ gfValueB;

		internal int Subtraction(int gfValueA, int gfValueB) =>
			//Subtraction is same as addition.
			Addition(gfValueA, gfValueB);

		/// <returns>
		/// Product of two values.
		/// In other words. a multiply b
		/// </returns>
		internal int Product(int gfValueA, int gfValueB)
		{
			if (gfValueA == 0 || gfValueB == 0)
			{
				return 0;
			}
			if (gfValueA == 1)
			{
				return gfValueB;
			}
			if (gfValueB == 1)
			{
				return gfValueA;
			}

			return Exponent((Log(gfValueA) + Log(gfValueB)) % 255);
		}

		/// <returns>
		/// Quotient of two values.
		/// In other words. a devided b
		/// </returns>
		internal int Quotient(int gfValueA, int gfValueB)
		{
			if (gfValueA == 0)
				return 0;
			if (gfValueB == 0)
				throw new ArgumentException("gfValueB can not be zero");
			if (gfValueB == 1)
				return gfValueA;
			return Exponent(Math.Abs(Log(gfValueA) - Log(gfValueB)) % 255);
		}
	}
}
