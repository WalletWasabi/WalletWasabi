using System;

namespace Gma.QrCodeNet.Encoding.Positioning.Stencils
{
	internal class TimingPattern : PatternStencilBase
	{
		public TimingPattern(int version)
			: base(version)
		{
		}

		public override bool[,] Stencil => throw new NotImplementedException();

		public override void ApplyTo(TriStateMatrix matrix)
		{
			// -8 is for skipping position detection patterns (size 7), and two horizontal/vertical
			// separation patterns (size 1). Thus, 8 = 7 + 1.
			for (int i = 8; i < matrix.Width - 8; ++i)
			{
				bool value = (sbyte)((i + 1) % 2) == 1;
				// Horizontal line.

				if (matrix.MStatus(6, i) == MatrixStatus.None)
				{
					matrix[6, i, MatrixStatus.NoMask] = value;
				}

				// Vertical line.
				if (matrix.MStatus(i, 6) == MatrixStatus.None)
				{
					matrix[i, 6, MatrixStatus.NoMask] = value;
				}
			}
		}
	}
}
