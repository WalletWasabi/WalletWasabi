using System;

namespace Gma.QrCodeNet.Encoding.Versions
{
	public static class VersionTable
	{
		private static readonly QRCodeVersion[] Version = Initialize();

		internal static QRCodeVersion GetVersionByNum(int versionNum)
		{
			if (versionNum is < QRCodeConstantVariable.MinVersion or > QRCodeConstantVariable.MaxVersion)
			{
				throw new InvalidOperationException($"Unexpected version number: {versionNum}.");
			}

			return Version[versionNum - 1];
		}

		internal static QRCodeVersion GetVersionByWidth(int matrixWidth)
		{
			if ((matrixWidth - 17) % 4 != 0)
			{
				throw new ArgumentException("Incorrect matrix width.");
			}
			else
			{
				return GetVersionByNum((matrixWidth - 17) / 4);
			}
		}

		private static QRCodeVersion[] Initialize()
		{
			return new QRCodeVersion[]
			{
				new QRCodeVersion(
					1,
					26,
					new ErrorCorrectionBlocks(7, new ErrorCorrectionBlock(1, 19)),
					new ErrorCorrectionBlocks(10, new ErrorCorrectionBlock(1, 16)),
					new ErrorCorrectionBlocks(13, new ErrorCorrectionBlock(1, 13)),
					new ErrorCorrectionBlocks(17, new ErrorCorrectionBlock(1, 9))),
				new QRCodeVersion(
					2,
					44,
					new ErrorCorrectionBlocks(10, new ErrorCorrectionBlock(1, 34)),
					new ErrorCorrectionBlocks(16, new ErrorCorrectionBlock(1, 28)),
					new ErrorCorrectionBlocks(22, new ErrorCorrectionBlock(1, 22)),
					new ErrorCorrectionBlocks(28, new ErrorCorrectionBlock(1, 16))),
				new QRCodeVersion(
					3,
					70,
					new ErrorCorrectionBlocks(15, new ErrorCorrectionBlock(1, 55)),
					new ErrorCorrectionBlocks(26, new ErrorCorrectionBlock(1, 44)),
					new ErrorCorrectionBlocks(36, new ErrorCorrectionBlock(2, 17)),
					new ErrorCorrectionBlocks(44, new ErrorCorrectionBlock(2, 13))),
				new QRCodeVersion(
					4,
					100,
					new ErrorCorrectionBlocks(20, new ErrorCorrectionBlock(1, 80)),
					new ErrorCorrectionBlocks(36, new ErrorCorrectionBlock(2, 32)),
					new ErrorCorrectionBlocks(52, new ErrorCorrectionBlock(2, 24)),
					new ErrorCorrectionBlocks(64, new ErrorCorrectionBlock(4, 9))),
				new QRCodeVersion(
					5,
					134,
					new ErrorCorrectionBlocks(26, new ErrorCorrectionBlock(1, 108)),
					new ErrorCorrectionBlocks(48, new ErrorCorrectionBlock(2, 43)),
					new ErrorCorrectionBlocks(72, new ErrorCorrectionBlock(2, 15), new ErrorCorrectionBlock(2, 16)),
					new ErrorCorrectionBlocks(88, new ErrorCorrectionBlock(2, 11), new ErrorCorrectionBlock(2, 12))),
				new QRCodeVersion(
					6,
					172,
					new ErrorCorrectionBlocks(36, new ErrorCorrectionBlock(2, 68)),
					new ErrorCorrectionBlocks(64, new ErrorCorrectionBlock(4, 27)),
					new ErrorCorrectionBlocks(96, new ErrorCorrectionBlock(4, 19)),
					new ErrorCorrectionBlocks(112, new ErrorCorrectionBlock(4, 15))),
				new QRCodeVersion(
					7,
					196,
					new ErrorCorrectionBlocks(40, new ErrorCorrectionBlock(2, 78)),
					new ErrorCorrectionBlocks(72, new ErrorCorrectionBlock(4, 31)),
					new ErrorCorrectionBlocks(108, new ErrorCorrectionBlock(2, 14), new ErrorCorrectionBlock(4, 15)),
					new ErrorCorrectionBlocks(130, new ErrorCorrectionBlock(4, 13), new ErrorCorrectionBlock(1, 14))),
				new QRCodeVersion(
					8,
					242,
					new ErrorCorrectionBlocks(48, new ErrorCorrectionBlock(2, 97)),
					new ErrorCorrectionBlocks(88, new ErrorCorrectionBlock(2, 38), new ErrorCorrectionBlock(2, 39)),
					new ErrorCorrectionBlocks(132, new ErrorCorrectionBlock(4, 18), new ErrorCorrectionBlock(2, 19)),
					new ErrorCorrectionBlocks(156, new ErrorCorrectionBlock(4, 14), new ErrorCorrectionBlock(2, 15))),
				new QRCodeVersion(
					9,
					292,
					new ErrorCorrectionBlocks(60, new ErrorCorrectionBlock(2, 116)),
					new ErrorCorrectionBlocks(110, new ErrorCorrectionBlock(3, 36), new ErrorCorrectionBlock(2, 37)),
					new ErrorCorrectionBlocks(160, new ErrorCorrectionBlock(4, 16), new ErrorCorrectionBlock(4, 17)),
					new ErrorCorrectionBlocks(192, new ErrorCorrectionBlock(4, 12), new ErrorCorrectionBlock(4, 13))),
				new QRCodeVersion(
					10,
					346,
					new ErrorCorrectionBlocks(72, new ErrorCorrectionBlock(2, 68), new ErrorCorrectionBlock(2, 69)),
					new ErrorCorrectionBlocks(130, new ErrorCorrectionBlock(4, 43), new ErrorCorrectionBlock(1, 44)),
					new ErrorCorrectionBlocks(192, new ErrorCorrectionBlock(6, 19), new ErrorCorrectionBlock(2, 20)),
					new ErrorCorrectionBlocks(224, new ErrorCorrectionBlock(6, 15), new ErrorCorrectionBlock(2, 16))),
				new QRCodeVersion(
					11,
					404,
					new ErrorCorrectionBlocks(80, new ErrorCorrectionBlock(4, 81)),
					new ErrorCorrectionBlocks(150, new ErrorCorrectionBlock(1, 50), new ErrorCorrectionBlock(4, 51)),
					new ErrorCorrectionBlocks(224, new ErrorCorrectionBlock(4, 22), new ErrorCorrectionBlock(4, 23)),
					new ErrorCorrectionBlocks(264, new ErrorCorrectionBlock(3, 12), new ErrorCorrectionBlock(8, 13))),
				new QRCodeVersion(
					12,
					466,
					new ErrorCorrectionBlocks(96, new ErrorCorrectionBlock(2, 92), new ErrorCorrectionBlock(2, 93)),
					new ErrorCorrectionBlocks(176, new ErrorCorrectionBlock(6, 36), new ErrorCorrectionBlock(2, 37)),
					new ErrorCorrectionBlocks(260, new ErrorCorrectionBlock(4, 20), new ErrorCorrectionBlock(6, 21)),
					new ErrorCorrectionBlocks(308, new ErrorCorrectionBlock(7, 14), new ErrorCorrectionBlock(4, 15))),
				new QRCodeVersion(
					13,
					532,
					new ErrorCorrectionBlocks(104, new ErrorCorrectionBlock(4, 107)),
					new ErrorCorrectionBlocks(198, new ErrorCorrectionBlock(8, 37), new ErrorCorrectionBlock(1, 38)),
					new ErrorCorrectionBlocks(288, new ErrorCorrectionBlock(8, 20), new ErrorCorrectionBlock(4, 21)),
					new ErrorCorrectionBlocks(352, new ErrorCorrectionBlock(12, 11), new ErrorCorrectionBlock(4, 12))),
				new QRCodeVersion(
					14,
					581,
					new ErrorCorrectionBlocks(120, new ErrorCorrectionBlock(3, 115), new ErrorCorrectionBlock(1, 116)),
					new ErrorCorrectionBlocks(216, new ErrorCorrectionBlock(4, 40), new ErrorCorrectionBlock(5, 41)),
					new ErrorCorrectionBlocks(320, new ErrorCorrectionBlock(11, 16), new ErrorCorrectionBlock(5, 17)),
					new ErrorCorrectionBlocks(384, new ErrorCorrectionBlock(11, 12), new ErrorCorrectionBlock(5, 13))),
				new QRCodeVersion(
					15,
					655,
					new ErrorCorrectionBlocks(132, new ErrorCorrectionBlock(5, 87), new ErrorCorrectionBlock(1, 88)),
					new ErrorCorrectionBlocks(240, new ErrorCorrectionBlock(5, 41), new ErrorCorrectionBlock(5, 42)),
					new ErrorCorrectionBlocks(360, new ErrorCorrectionBlock(5, 24), new ErrorCorrectionBlock(7, 25)),
					new ErrorCorrectionBlocks(432, new ErrorCorrectionBlock(11, 12), new ErrorCorrectionBlock(7, 13))),
				new QRCodeVersion(
					16,
					733,
					new ErrorCorrectionBlocks(144, new ErrorCorrectionBlock(5, 98), new ErrorCorrectionBlock(1, 99)),
					new ErrorCorrectionBlocks(280, new ErrorCorrectionBlock(7, 45), new ErrorCorrectionBlock(3, 46)),
					new ErrorCorrectionBlocks(408, new ErrorCorrectionBlock(15, 19), new ErrorCorrectionBlock(2, 20)),
					new ErrorCorrectionBlocks(480, new ErrorCorrectionBlock(3, 15), new ErrorCorrectionBlock(13, 16))),
				new QRCodeVersion(
					17,
					815,
					new ErrorCorrectionBlocks(168, new ErrorCorrectionBlock(1, 107), new ErrorCorrectionBlock(5, 108)),
					new ErrorCorrectionBlocks(308, new ErrorCorrectionBlock(10, 46), new ErrorCorrectionBlock(1, 47)),
					new ErrorCorrectionBlocks(448, new ErrorCorrectionBlock(1, 22), new ErrorCorrectionBlock(15, 23)),
					new ErrorCorrectionBlocks(532, new ErrorCorrectionBlock(2, 14), new ErrorCorrectionBlock(17, 15))),
				new QRCodeVersion(
					18,
					901,
					new ErrorCorrectionBlocks(180, new ErrorCorrectionBlock(5, 120), new ErrorCorrectionBlock(1, 121)),
					new ErrorCorrectionBlocks(338, new ErrorCorrectionBlock(9, 43), new ErrorCorrectionBlock(4, 44)),
					new ErrorCorrectionBlocks(504, new ErrorCorrectionBlock(17, 22), new ErrorCorrectionBlock(1, 23)),
					new ErrorCorrectionBlocks(588, new ErrorCorrectionBlock(2, 14), new ErrorCorrectionBlock(19, 15))),
				new QRCodeVersion(
					19,
					991,
					new ErrorCorrectionBlocks(196, new ErrorCorrectionBlock(3, 113), new ErrorCorrectionBlock(4, 114)),
					new ErrorCorrectionBlocks(364, new ErrorCorrectionBlock(3, 44), new ErrorCorrectionBlock(11, 45)),
					new ErrorCorrectionBlocks(546, new ErrorCorrectionBlock(17, 21), new ErrorCorrectionBlock(4, 22)),
					new ErrorCorrectionBlocks(650, new ErrorCorrectionBlock(9, 13), new ErrorCorrectionBlock(16, 14))),
				new QRCodeVersion(
					20,
					1085,
					new ErrorCorrectionBlocks(224, new ErrorCorrectionBlock(3, 107), new ErrorCorrectionBlock(5, 108)),
					new ErrorCorrectionBlocks(416, new ErrorCorrectionBlock(3, 41), new ErrorCorrectionBlock(13, 42)),
					new ErrorCorrectionBlocks(600, new ErrorCorrectionBlock(15, 24), new ErrorCorrectionBlock(5, 25)),
					new ErrorCorrectionBlocks(700, new ErrorCorrectionBlock(15, 15), new ErrorCorrectionBlock(10, 16))),
				new QRCodeVersion(
					21,
					1156,
					new ErrorCorrectionBlocks(224, new ErrorCorrectionBlock(4, 116), new ErrorCorrectionBlock(4, 117)),
					new ErrorCorrectionBlocks(442, new ErrorCorrectionBlock(17, 42)),
					new ErrorCorrectionBlocks(644, new ErrorCorrectionBlock(17, 22), new ErrorCorrectionBlock(6, 23)),
					new ErrorCorrectionBlocks(750, new ErrorCorrectionBlock(19, 16), new ErrorCorrectionBlock(6, 17))),
				new QRCodeVersion(
					22,
					1258,
					new ErrorCorrectionBlocks(252, new ErrorCorrectionBlock(2, 111), new ErrorCorrectionBlock(7, 112)),
					new ErrorCorrectionBlocks(476, new ErrorCorrectionBlock(17, 46)),
					new ErrorCorrectionBlocks(690, new ErrorCorrectionBlock(7, 24), new ErrorCorrectionBlock(16, 25)),
					new ErrorCorrectionBlocks(816, new ErrorCorrectionBlock(34, 13))),
				new QRCodeVersion(
					23,
					1364,
					new ErrorCorrectionBlocks(270, new ErrorCorrectionBlock(4, 121), new ErrorCorrectionBlock(5, 122)),
					new ErrorCorrectionBlocks(504, new ErrorCorrectionBlock(4, 47), new ErrorCorrectionBlock(14, 48)),
					new ErrorCorrectionBlocks(750, new ErrorCorrectionBlock(11, 24), new ErrorCorrectionBlock(14, 25)),
					new ErrorCorrectionBlocks(900, new ErrorCorrectionBlock(16, 15), new ErrorCorrectionBlock(14, 16))),
				new QRCodeVersion(
					24,
					1474,
					new ErrorCorrectionBlocks(300, new ErrorCorrectionBlock(6, 117), new ErrorCorrectionBlock(4, 118)),
					new ErrorCorrectionBlocks(560, new ErrorCorrectionBlock(6, 45), new ErrorCorrectionBlock(14, 46)),
					new ErrorCorrectionBlocks(810, new ErrorCorrectionBlock(11, 24), new ErrorCorrectionBlock(16, 25)),
					new ErrorCorrectionBlocks(960, new ErrorCorrectionBlock(30, 16), new ErrorCorrectionBlock(2, 17))),
				new QRCodeVersion(
					25,
					1588,
					new ErrorCorrectionBlocks(312, new ErrorCorrectionBlock(8, 106), new ErrorCorrectionBlock(4, 107)),
					new ErrorCorrectionBlocks(588, new ErrorCorrectionBlock(8, 47), new ErrorCorrectionBlock(13, 48)),
					new ErrorCorrectionBlocks(870, new ErrorCorrectionBlock(7, 24), new ErrorCorrectionBlock(22, 25)),
					new ErrorCorrectionBlocks(1050, new ErrorCorrectionBlock(22, 15), new ErrorCorrectionBlock(13, 16))),
				new QRCodeVersion(
					26,
					1706,
					new ErrorCorrectionBlocks(336, new ErrorCorrectionBlock(10, 114), new ErrorCorrectionBlock(2, 115)),
					new ErrorCorrectionBlocks(644, new ErrorCorrectionBlock(19, 46), new ErrorCorrectionBlock(4, 47)),
					new ErrorCorrectionBlocks(952, new ErrorCorrectionBlock(28, 22), new ErrorCorrectionBlock(6, 23)),
					new ErrorCorrectionBlocks(1110, new ErrorCorrectionBlock(33, 16), new ErrorCorrectionBlock(4, 17))),
				new QRCodeVersion(
					27,
					1828,
					new ErrorCorrectionBlocks(360, new ErrorCorrectionBlock(8, 122), new ErrorCorrectionBlock(4, 123)),
					new ErrorCorrectionBlocks(700, new ErrorCorrectionBlock(22, 45), new ErrorCorrectionBlock(3, 46)),
					new ErrorCorrectionBlocks(1020, new ErrorCorrectionBlock(8, 23), new ErrorCorrectionBlock(26, 24)),
					new ErrorCorrectionBlocks(1200, new ErrorCorrectionBlock(12, 15), new ErrorCorrectionBlock(28, 16))),
				new QRCodeVersion(
					28,
					1921,
					new ErrorCorrectionBlocks(390, new ErrorCorrectionBlock(3, 117), new ErrorCorrectionBlock(10, 118)),
					new ErrorCorrectionBlocks(728, new ErrorCorrectionBlock(3, 45), new ErrorCorrectionBlock(23, 46)),
					new ErrorCorrectionBlocks(1050, new ErrorCorrectionBlock(4, 24), new ErrorCorrectionBlock(31, 25)),
					new ErrorCorrectionBlocks(1260, new ErrorCorrectionBlock(11, 15), new ErrorCorrectionBlock(31, 16))),
				new QRCodeVersion(
					29,
					2051,
					new ErrorCorrectionBlocks(420, new ErrorCorrectionBlock(7, 116), new ErrorCorrectionBlock(7, 117)),
					new ErrorCorrectionBlocks(784, new ErrorCorrectionBlock(21, 45), new ErrorCorrectionBlock(7, 46)),
					new ErrorCorrectionBlocks(1140, new ErrorCorrectionBlock(1, 23), new ErrorCorrectionBlock(37, 24)),
					new ErrorCorrectionBlocks(1350, new ErrorCorrectionBlock(19, 15), new ErrorCorrectionBlock(26, 16))),
				new QRCodeVersion(
					30,
					2185,
					new ErrorCorrectionBlocks(450, new ErrorCorrectionBlock(5, 115), new ErrorCorrectionBlock(10, 116)),
					new ErrorCorrectionBlocks(812, new ErrorCorrectionBlock(19, 47), new ErrorCorrectionBlock(10, 48)),
					new ErrorCorrectionBlocks(1200, new ErrorCorrectionBlock(15, 24), new ErrorCorrectionBlock(25, 25)),
					new ErrorCorrectionBlocks(1440, new ErrorCorrectionBlock(23, 15), new ErrorCorrectionBlock(25, 16))),
				new QRCodeVersion(
					31,
					2323,
					new ErrorCorrectionBlocks(480, new ErrorCorrectionBlock(13, 115), new ErrorCorrectionBlock(3, 116)),
					new ErrorCorrectionBlocks(868, new ErrorCorrectionBlock(2, 46), new ErrorCorrectionBlock(29, 47)),
					new ErrorCorrectionBlocks(1290, new ErrorCorrectionBlock(42, 24), new ErrorCorrectionBlock(1, 25)),
					new ErrorCorrectionBlocks(1530, new ErrorCorrectionBlock(23, 15), new ErrorCorrectionBlock(28, 16))),
				new QRCodeVersion(
					32,
					2465,
					new ErrorCorrectionBlocks(510, new ErrorCorrectionBlock(17, 115)),
					new ErrorCorrectionBlocks(924, new ErrorCorrectionBlock(10, 46), new ErrorCorrectionBlock(23, 47)),
					new ErrorCorrectionBlocks(1350, new ErrorCorrectionBlock(10, 24), new ErrorCorrectionBlock(35, 25)),
					new ErrorCorrectionBlocks(1620, new ErrorCorrectionBlock(19, 15), new ErrorCorrectionBlock(35, 16))),
				new QRCodeVersion(
					33,
					2611,
					new ErrorCorrectionBlocks(540, new ErrorCorrectionBlock(17, 115), new ErrorCorrectionBlock(1, 116)),
					new ErrorCorrectionBlocks(980, new ErrorCorrectionBlock(14, 46), new ErrorCorrectionBlock(21, 47)),
					new ErrorCorrectionBlocks(1440, new ErrorCorrectionBlock(29, 24), new ErrorCorrectionBlock(19, 25)),
					new ErrorCorrectionBlocks(1710, new ErrorCorrectionBlock(11, 15), new ErrorCorrectionBlock(46, 16))),
				new QRCodeVersion(
					34,
					2761,
					new ErrorCorrectionBlocks(570, new ErrorCorrectionBlock(13, 115), new ErrorCorrectionBlock(6, 116)),
					new ErrorCorrectionBlocks(1036, new ErrorCorrectionBlock(14, 46), new ErrorCorrectionBlock(23, 47)),
					new ErrorCorrectionBlocks(1530, new ErrorCorrectionBlock(44, 24), new ErrorCorrectionBlock(7, 25)),
					new ErrorCorrectionBlocks(1800, new ErrorCorrectionBlock(59, 16), new ErrorCorrectionBlock(1, 17))),
				new QRCodeVersion(
					35,
					2876,
					new ErrorCorrectionBlocks(570, new ErrorCorrectionBlock(12, 121), new ErrorCorrectionBlock(7, 122)),
					new ErrorCorrectionBlocks(1064, new ErrorCorrectionBlock(12, 47), new ErrorCorrectionBlock(26, 48)),
					new ErrorCorrectionBlocks(1590, new ErrorCorrectionBlock(39, 24), new ErrorCorrectionBlock(14, 25)),
					new ErrorCorrectionBlocks(1890, new ErrorCorrectionBlock(22, 15), new ErrorCorrectionBlock(41, 16))),
				new QRCodeVersion(
					36,
					3034,
					new ErrorCorrectionBlocks(600, new ErrorCorrectionBlock(6, 121), new ErrorCorrectionBlock(14, 122)),
					new ErrorCorrectionBlocks(1120, new ErrorCorrectionBlock(6, 47), new ErrorCorrectionBlock(34, 48)),
					new ErrorCorrectionBlocks(1680, new ErrorCorrectionBlock(46, 24), new ErrorCorrectionBlock(10, 25)),
					new ErrorCorrectionBlocks(1980, new ErrorCorrectionBlock(2, 15), new ErrorCorrectionBlock(64, 16))),
				new QRCodeVersion(
					37,
					3196,
					new ErrorCorrectionBlocks(630, new ErrorCorrectionBlock(17, 122), new ErrorCorrectionBlock(4, 123)),
					new ErrorCorrectionBlocks(1204, new ErrorCorrectionBlock(29, 46), new ErrorCorrectionBlock(14, 47)),
					new ErrorCorrectionBlocks(1770, new ErrorCorrectionBlock(49, 24), new ErrorCorrectionBlock(10, 25)),
					new ErrorCorrectionBlocks(2100, new ErrorCorrectionBlock(24, 15), new ErrorCorrectionBlock(46, 16))),
				new QRCodeVersion(
					38,
					3362,
					new ErrorCorrectionBlocks(660, new ErrorCorrectionBlock(4, 122), new ErrorCorrectionBlock(18, 123)),
					new ErrorCorrectionBlocks(1260, new ErrorCorrectionBlock(13, 46), new ErrorCorrectionBlock(32, 47)),
					new ErrorCorrectionBlocks(1860, new ErrorCorrectionBlock(48, 24), new ErrorCorrectionBlock(14, 25)),
					new ErrorCorrectionBlocks(2220, new ErrorCorrectionBlock(42, 15), new ErrorCorrectionBlock(32, 16))),
				new QRCodeVersion(
					39,
					3532,
					new ErrorCorrectionBlocks(720, new ErrorCorrectionBlock(20, 117), new ErrorCorrectionBlock(4, 118)),
					new ErrorCorrectionBlocks(1316, new ErrorCorrectionBlock(40, 47), new ErrorCorrectionBlock(7, 48)),
					new ErrorCorrectionBlocks(1950, new ErrorCorrectionBlock(43, 24), new ErrorCorrectionBlock(22, 25)),
					new ErrorCorrectionBlocks(2310, new ErrorCorrectionBlock(10, 15), new ErrorCorrectionBlock(67, 16))),
				new QRCodeVersion(
					40,
					3706,
					new ErrorCorrectionBlocks(750, new ErrorCorrectionBlock(19, 118), new ErrorCorrectionBlock(6, 119)),
					new ErrorCorrectionBlocks(1372, new ErrorCorrectionBlock(18, 47), new ErrorCorrectionBlock(31, 48)),
					new ErrorCorrectionBlocks(2040, new ErrorCorrectionBlock(34, 24), new ErrorCorrectionBlock(34, 25)),
					new ErrorCorrectionBlocks(2430, new ErrorCorrectionBlock(20, 15), new ErrorCorrectionBlock(61, 16))),
			};
		}
	}
}
