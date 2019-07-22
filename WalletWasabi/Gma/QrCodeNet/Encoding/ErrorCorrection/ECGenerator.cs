using Gma.QrCodeNet.Encoding.ReedSolomon;
using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.ErrorCorrection
{
	internal static class ECGenerator
	{
		internal static BitList FillECCodewords(BitList dataCodewords, VersionDetail vd)
		{
			List<byte> dataCodewordsByte = dataCodewords.List;
			int ecBlockGroup2 = vd.ECBlockGroup2;
			int ecBlockGroup1 = vd.ECBlockGroup1;
			int numDataBytesGroup1 = vd.NumDataBytesGroup1;
			int numDataBytesGroup2 = vd.NumDataBytesGroup2;

			int ecBytesPerBlock = vd.NumECBytesPerBlock;

			int dataBytesOffset = 0;
			byte[][] dByteJArray = new byte[vd.NumECBlocks][];
			byte[][] ecByteJArray = new byte[vd.NumECBlocks][];

			GaloisField256 gf256 = GaloisField256.QRCodeGaloisField;
			GeneratorPolynomial generator = new GeneratorPolynomial(gf256);

			for (int blockId = 0; blockId < vd.NumECBlocks; blockId++)
			{
				if (blockId < ecBlockGroup1)
				{
					dByteJArray[blockId] = new byte[numDataBytesGroup1];
					for (int index = 0; index < numDataBytesGroup1; index++)
					{
						dByteJArray[blockId][index] = dataCodewordsByte[dataBytesOffset + index];
					}
					dataBytesOffset += numDataBytesGroup1;
				}
				else
				{
					dByteJArray[blockId] = new byte[numDataBytesGroup2];
					for (int index = 0; index < numDataBytesGroup2; index++)
					{
						dByteJArray[blockId][index] = dataCodewordsByte[dataBytesOffset + index];
					}
					dataBytesOffset += numDataBytesGroup2;
				}

				ecByteJArray[blockId] = ReedSolomonEncoder.Encode(dByteJArray[blockId], ecBytesPerBlock, generator);
			}
			if (vd.NumDataBytes != dataBytesOffset)
			{
				throw new ArgumentException("Data bytes does not match offset");
			}

			BitList codewords = new BitList();

			int maxDataLength = ecBlockGroup1 == vd.NumECBlocks ? numDataBytesGroup1 : numDataBytesGroup2;

			for (int dataId = 0; dataId < maxDataLength; dataId++)
			{
				for (int blockId = 0; blockId < vd.NumECBlocks; blockId++)
				{
					if (!(dataId == numDataBytesGroup1 && blockId < ecBlockGroup1))
					{
						codewords.Add(dByteJArray[blockId][dataId], 8);
					}
				}
			}

			for (int ECID = 0; ECID < ecBytesPerBlock; ECID++)
			{
				for (int blockId = 0; blockId < vd.NumECBlocks; blockId++)
				{
					codewords.Add(ecByteJArray[blockId][ECID], 8);
				}
			}

			if (vd.NumTotalBytes != codewords.Count >> 3)
			{
				throw new ArgumentException($"total bytes: {vd.NumTotalBytes}, actual bits: {codewords.Count}");
			}

			return codewords;
		}
	}
}
