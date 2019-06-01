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

			for (int blockID = 0; blockID < vd.NumECBlocks; blockID++)
			{
				if (blockID < ecBlockGroup1)
				{
					dByteJArray[blockID] = new byte[numDataBytesGroup1];
					for (int index = 0; index < numDataBytesGroup1; index++)
					{
						dByteJArray[blockID][index] = dataCodewordsByte[dataBytesOffset + index];
					}
					dataBytesOffset += numDataBytesGroup1;
				}
				else
				{
					dByteJArray[blockID] = new byte[numDataBytesGroup2];
					for (int index = 0; index < numDataBytesGroup2; index++)
					{
						dByteJArray[blockID][index] = dataCodewordsByte[dataBytesOffset + index];
					}
					dataBytesOffset += numDataBytesGroup2;
				}

				ecByteJArray[blockID] = ReedSolomonEncoder.Encode(dByteJArray[blockID], ecBytesPerBlock, generator);
			}
			if (vd.NumDataBytes != dataBytesOffset)
			{
				throw new ArgumentException("Data bytes does not match offset");
			}

			BitList codewords = new BitList();

			int maxDataLength = ecBlockGroup1 == vd.NumECBlocks ? numDataBytesGroup1 : numDataBytesGroup2;

			for (int dataID = 0; dataID < maxDataLength; dataID++)
			{
				for (int blockID = 0; blockID < vd.NumECBlocks; blockID++)
				{
					if (!(dataID == numDataBytesGroup1 && blockID < ecBlockGroup1))
					{
						codewords.Add(dByteJArray[blockID][dataID], 8);
					}
				}
			}

			for (int ECID = 0; ECID < ecBytesPerBlock; ECID++)
			{
				for (int blockID = 0; blockID < vd.NumECBlocks; blockID++)
				{
					codewords.Add(ecByteJArray[blockID][ECID], 8);
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
