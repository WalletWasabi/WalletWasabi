﻿using System;
using Gma.QrCodeNet.Encoding.EncodingRegion;

namespace Gma.QrCodeNet.Encoding.Masking
{
	public static class MatrixExtensions
	{
		public static TriStateMatrix Xor(this TriStateMatrix first, Pattern second, ErrorCorrectionLevel errorlevel)
		{
			TriStateMatrix result = XorMatrix(first, second);
			result.EmbedFormatInformation(errorlevel, second);
			return result;
		}

		private static TriStateMatrix XorMatrix(TriStateMatrix first, BitMatrix second)
		{
			int width = first.Width;
			TriStateMatrix maskedMatrix = new TriStateMatrix(width);
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < width; y++)
				{
					MatrixStatus states = first.MStatus(x, y);
					switch (states)
					{
						case MatrixStatus.NoMask:
							maskedMatrix[x, y, MatrixStatus.NoMask] = first[x, y];
							break;

						case MatrixStatus.Data:
							maskedMatrix[x, y, MatrixStatus.Data] = first[x, y] ^ second[x, y];
							break;

						default:
							throw new ArgumentException("TristateMatrix has None value cell.", nameof(first));
					}
				}
			}

			return maskedMatrix;
		}

		public static TriStateMatrix Apply(this TriStateMatrix matrix, Pattern pattern, ErrorCorrectionLevel errorlevel) => matrix.Xor(pattern, errorlevel);

		public static TriStateMatrix Apply(this Pattern pattern, TriStateMatrix matrix, ErrorCorrectionLevel errorlevel) => matrix.Xor(pattern, errorlevel);
	}
}
