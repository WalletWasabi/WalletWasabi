/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code error correction calculations.
//
//	Author: Uzi Granot
//	Original Version: 1.0
//	Date: June 30, 2018
//	Copyright (C) 2018-2019 Uzi Granot. All Rights Reserved
//	For full version history please look at QRDecoder.cs
//
//	QR Code Library C# class library and the attached test/demo
//  applications are free software.
//	Software developed by this author is licensed under CPOL 1.02.
//	Some portions of the QRCodeVideoDecoder are licensed under GNU Lesser
//	General Public License v3.0.
//
//	The solution is made of 3 projects:
//	1. QRCodeDecoderLibrary: QR code decoding.
//	3. QRCodeDecoderDemo: Decode QR code image files.
//	4. QRCodeVideoDecoder: Decode QR code using web camera.
//		This demo program is using some of the source modules of
//		Camera_Net project published at CodeProject.com:
//		https://www.codeproject.com/Articles/671407/Camera_Net-Library
//		and at GitHub: https://github.com/free5lot/Camera_Net.
//		This project is based on DirectShowLib.
//		http://sourceforge.net/projects/directshownet/
//		This project includes a modified subset of the source modules.
//
//	The main points of CPOL 1.02 subject to the terms of the License are:
//
//	Source Code and Executable Files can be used in commercial applications;
//	Source Code and Executable Files can be redistributed; and
//	Source Code can be modified to create derivative works.
//	No claim of suitability, guarantee, or any warranty whatsoever is
//	provided. The software is provided "as-is".
//	The Article accompanying the Work may not be distributed or republished
//	without the Author's consent
//
//	For version history please refer to QRDecoder.cs
/////////////////////////////////////////////////////////////////////

using System;

namespace QRCodeDecoderLibrary
{
	internal class ReedSolomon
	{
		internal static int INCORRECTABLE_ERROR = -1;

		internal static int CorrectData
				(
				byte[] receivedData,        // recived data buffer with data and error correction code
				int dataLength,         // length of data in the buffer (note sometimes the array is longer than data)
				int errCorrCodewords    // numer of error correction codewords
				)
		{
			// calculate syndrome vector
			int[]? syndrome = CalculateSyndrome(receivedData, dataLength, errCorrCodewords);

			// received data has no error
			// note: this should not happen because we call this method only if error was detected
			if (syndrome == null)
			{
				return 0;
			}

			// Modified Berlekamp-Massey
			// calculate sigma and omega
			int[] sigma = new int[errCorrCodewords / 2 + 2];
			int[] omega = new int[errCorrCodewords / 2 + 1];
			int errorCount = CalculateSigmaMBM(sigma, omega, syndrome, errCorrCodewords);

			// data cannot be corrected
			if (errorCount <= 0)
			{
				return INCORRECTABLE_ERROR;
			}

			// look for error position using Chien search
			int[] errorPosition = new int[errorCount];
			if (!ChienSearch(errorPosition, dataLength, errorCount, sigma))
			{
				return INCORRECTABLE_ERROR;
			}

			// correct data array based on position array
			ApplyCorrection(receivedData, dataLength, errorCount, errorPosition, sigma, omega);

			// return error count before it was corrected
			return errorCount;
		}

		// Syndrome vector calculation
		// S0 = R0 + R1 +        R2 + ....        + Rn
		// S1 = R0 + R1 * A**1 + R2 * A**2 + .... + Rn * A**n
		// S2 = R0 + R1 * A**2 + R2 * A**4 + .... + Rn * A**2n
		// ....
		// Sm = R0 + R1 * A**m + R2 * A**2m + .... + Rn * A**mn

		internal static int[]? CalculateSyndrome
				(
				byte[] receivedData,        // recived data buffer with data and error correction code
				int dataLength,         // length of data in the buffer (note sometimes the array is longer than data)
				int errCorrCodewords    // numer of error correction codewords
				)
		{
			// allocate syndrome vector
			int[] syndrome = new int[errCorrCodewords];

			// reset error indicator
			bool error = false;

			// syndrome[zero] special case
			// Total = Data[0] + Data[1] + ... Data[n]
			int total = receivedData[0];
			for (int sumIndex = 1; sumIndex < dataLength; sumIndex++)
			{
				total = receivedData[sumIndex] ^ total;
			}

			syndrome[0] = total;
			if (total != 0)
			{
				error = true;
			}

			// all other synsromes
			for (int index = 1; index < errCorrCodewords; index++)
			{
				// Total = Data[0] + Data[1] * Alpha + Data[2] * Alpha ** 2 + ... Data[n] * Alpha ** n
				total = receivedData[0];
				for (int indexT = 1; indexT < dataLength; indexT++)
				{
					total = receivedData[indexT] ^ MultiplyIntByExp(total, index);
				}

				syndrome[index] = total;
				if (total != 0)
				{
					error = true;
				}
			}

			// if there is an error return syndrome vector otherwise return null
			return error ? syndrome : null;
		}

		// Modified Berlekamp-Massey
		internal static int CalculateSigmaMBM
				(
				int[] sigma,
				int[] omega,
				int[] syndrome,
				int errCorrCodewords
				)
		{
			int[] polyC = new int[errCorrCodewords];
			int[] polyB = new int[errCorrCodewords];
			polyC[1] = 1;
			polyB[0] = 1;
			int errorControl = 1;
			int errorCount = 0;
			int m = -1;

			for (int errCorrIndex = 0; errCorrIndex < errCorrCodewords; errCorrIndex++)
			{
				// Calculate the discrepancy
				int dis = syndrome[errCorrIndex];
				for (int i = 1; i <= errorCount; i++)
				{
					dis ^= Multiply(polyB[i], syndrome[errCorrIndex - i]);
				}

				if (dis != 0)
				{
					int disExp = StaticTables.IntToExp[dis];
					int[] workPolyB = new int[errCorrCodewords];
					for (int index = 0; index <= errCorrIndex; index++)
					{
						workPolyB[index] = polyB[index] ^ MultiplyIntByExp(polyC[index], disExp);
					}

					int js = errCorrIndex - m;
					if (js > errorCount)
					{
						m = errCorrIndex - errorCount;
						errorCount = js;
						if (errorCount > errCorrCodewords / 2)
						{
							return INCORRECTABLE_ERROR;
						}

						for (int index = 0; index <= errorControl; index++)
						{
							polyC[index] = DivideIntByExp(polyB[index], disExp);
						}

						errorControl = errorCount;
					}
					polyB = workPolyB;
				}

				// shift polynomial right one
				Array.Copy(polyC, 0, polyC, 1, Math.Min(polyC.Length - 1, errorControl));
				polyC[0] = 0;
				errorControl++;
			}

			PolynomialMultiply(omega, polyB, syndrome);
			Array.Copy(polyB, 0, sigma, 0, Math.Min(polyB.Length, sigma.Length));
			return errorCount;
		}

		// Chien search is a fast algorithm for determining roots of polynomials defined over a finite field.
		// The most typical use of the Chien search is in finding the roots of error-locator polynomials
		// encountered in decoding Reed-Solomon codes and BCH codes.
		private static bool ChienSearch
				(
				int[] errorPosition,
				int dataLength,
				int errorCount,
				int[] sigma
				)
		{
			// last error
			int lastPosition = sigma[1];

			// one error
			if (errorCount == 1)
			{
				// position is out of range
				if (StaticTables.IntToExp[lastPosition] >= dataLength)
				{
					return false;
				}

				// save the only error position in position array
				errorPosition[0] = lastPosition;
				return true;
			}

			// we start at last error position
			int posIndex = errorCount - 1;
			for (int dataIndex = 0; dataIndex < dataLength; dataIndex++)
			{
				int dataIndexInverse = 255 - dataIndex;
				int total = 1;
				for (int index = 1; index <= errorCount; index++)
				{
					total ^= MultiplyIntByExp(sigma[index], (dataIndexInverse * index) % 255);
				}

				if (total != 0)
				{
					continue;
				}

				int position = StaticTables.ExpToInt[dataIndex];
				lastPosition ^= position;
				errorPosition[posIndex--] = position;
				if (posIndex == 0)
				{
					// position is out of range
					if (StaticTables.IntToExp[lastPosition] >= dataLength)
					{
						return false;
					}

					errorPosition[0] = lastPosition;
					return true;
				}
			}

			// search failed
			return false;
		}

		private static void ApplyCorrection
				(
				byte[] receivedData,
				int dataLength,
				int errorCount,
				int[] errorPosition,
				int[] sigma,
				int[] omega
				)
		{
			if (receivedData is null)
			{
				throw new ArgumentNullException(nameof(receivedData));
			}

			if (errorPosition is null)
			{
				throw new ArgumentNullException(nameof(errorPosition));
			}

			if (sigma is null)
			{
				throw new ArgumentNullException(nameof(sigma));
			}

			if (omega is null)
			{
				throw new ArgumentNullException(nameof(omega));
			}

			for (int errIndex = 0; errIndex < errorCount; errIndex++)
			{
				int ps = errorPosition[errIndex];
				int zlog = 255 - StaticTables.IntToExp[ps];
				int omegaTotal = omega[0];
				for (int index = 1; index < errorCount; index++)
				{
					omegaTotal ^= MultiplyIntByExp(omega[index], (zlog * index) % 255);
				}

				int sigmaTotal = sigma[1];
				for (int j = 2; j < errorCount; j += 2)
				{
					sigmaTotal ^= MultiplyIntByExp(sigma[j + 1], (zlog * j) % 255);
				}

				receivedData[dataLength - 1 - StaticTables.IntToExp[ps]] ^= (byte)MultiplyDivide(ps, omegaTotal, sigmaTotal);
			}
			return;
		}

		internal static void PolynominalDivision(byte[] polynomial, int polyLength, byte[] generator, int errCorrCodewords)
		{
			int dataCodewords = polyLength - errCorrCodewords;

			// error correction polynomial division
			for (int index = 0; index < dataCodewords; index++)
			{
				// current first codeword is zero
				if (polynomial[index] == 0)
				{
					continue;
				}

				// current first codeword is not zero
				int multiplier = StaticTables.IntToExp[polynomial[index]];

				// loop for error correction coofficients
				for (int generatorIndex = 0; generatorIndex < errCorrCodewords; generatorIndex++)
				{
					polynomial[index + 1 + generatorIndex] = (byte)(polynomial[index + 1 + generatorIndex] ^ StaticTables.ExpToInt[generator[generatorIndex] + multiplier]);
				}
			}
			return;
		}

		internal static int Multiply
				(
				int int1,
				int int2
				)
		{
			return (int1 == 0 || int2 == 0) ? 0 : StaticTables.ExpToInt[StaticTables.IntToExp[int1] + StaticTables.IntToExp[int2]];
		}

		internal static int MultiplyIntByExp
				(
				int integer,
				int exp
				)
		{
			return integer == 0 ? 0 : StaticTables.ExpToInt[StaticTables.IntToExp[integer] + exp];
		}

		internal static int MultiplyDivide
				(
				int int1,
				int int2,
				int int3
				)
		{
			return (int1 == 0 || int2 == 0) ? 0 : StaticTables.ExpToInt[(StaticTables.IntToExp[int1] + StaticTables.IntToExp[int2] - StaticTables.IntToExp[int3] + 255) % 255];
		}

		internal static int DivideIntByExp
				(
				int integer,
				int exp
				)
		{
			return integer == 0 ? 0 : StaticTables.ExpToInt[StaticTables.IntToExp[integer] - exp + 255];
		}

		internal static void PolynomialMultiply(int[] result, int[] poly1, int[] poly2)
		{
			Array.Clear(result, 0, result.Length);

			for (int index1 = 0; index1 < poly1.Length; index1++)
			{
				if (poly1[index1] == 0)
				{
					continue;
				}

				int loga = StaticTables.IntToExp[poly1[index1]];
				int index2End = Math.Min(poly2.Length, result.Length - index1);
				// = Sum(Poly1[Index1] * Poly2[Index2]) for all Index2
				for (int index2 = 0; index2 < index2End; index2++)
				{
					if (poly2[index2] != 0)
					{
						result[index1 + index2] ^= StaticTables.ExpToInt[loga + StaticTables.IntToExp[poly2[index2]]];
					}
				}
			}
			return;
		}
	}
}
