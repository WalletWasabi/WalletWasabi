namespace Gma.QrCodeNet.Encoding.Positioning.Stencils
{
	internal class PositionDetectionPattern : PatternStencilBase
	{
		public PositionDetectionPattern(int version)
			: base(version)
		{
		}

		private static bool[,] PositionDetection { get; } = new[,]
																		 {
																			 { O, O, O, O, O, O, O, O, O },
																			 { O, X, X, X, X, X, X, X, O },
																			 { O, X, O, O, O, O, O, X, O },
																			 { O, X, O, X, X, X, O, X, O },
																			 { O, X, O, X, X, X, O, X, O },
																			 { O, X, O, X, X, X, O, X, O },
																			 { O, X, O, O, O, O, O, X, O },
																			 { O, X, X, X, X, X, X, X, O },
																			 { O, O, O, O, O, O, O, O, O }
																		 };

		public override bool[,] Stencil => PositionDetection;

		public override void ApplyTo(TriStateMatrix matrix)
		{
			MatrixSize size = GetSizeOfSquareWithSeparators();

			MatrixPoint leftTopCorner = new MatrixPoint(0, 0);
			CopyTo(matrix, new MatrixRectangle(new MatrixPoint(1, 1), size), leftTopCorner, MatrixStatus.NoMask);

			MatrixPoint rightTopCorner = new MatrixPoint(matrix.Width - Width + 1, 0);
			CopyTo(matrix, new MatrixRectangle(new MatrixPoint(0, 1), size), rightTopCorner, MatrixStatus.NoMask);

			MatrixPoint leftBottomCorner = new MatrixPoint(0, matrix.Width - Width + 1);
			CopyTo(matrix, new MatrixRectangle(new MatrixPoint(1, 0), size), leftBottomCorner, MatrixStatus.NoMask);
		}

		private MatrixSize GetSizeOfSquareWithSeparators() => new MatrixSize(Width - 1, Height - 1);
	}
}
