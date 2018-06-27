using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.Positioning.Stencils
{
    internal class PositionDetectionPattern : PatternStencilBase
    {
        public PositionDetectionPattern(int version)
            : base(version)
        {
        }

        private static readonly bool[,] s_PositionDetection = new[,]
                                                                         {
                                                                             { o, o, o, o, o, o, o, o, o },
                                                                             { o, x, x, x, x, x, x, x, o }, 
                                                                             { o, x, o, o, o, o, o, x, o }, 
                                                                             { o, x, o, x, x, x, o, x, o }, 
                                                                             { o, x, o, x, x, x, o, x, o }, 
                                                                             { o, x, o, x, x, x, o, x, o }, 
                                                                             { o, x, o, o, o, o, o, x, o }, 
                                                                             { o, x, x, x, x, x, x, x, o },
                                                                             { o, o, o, o, o, o, o, o, o }
                                                                         };

        public override bool[,] Stencil
        {
            get { return s_PositionDetection; }
        }

        public override void ApplyTo(TriStateMatrix matrix)
        {
            MatrixSize size = GetSizeOfSquareWithSeparators();
            
            MatrixPoint leftTopCorner = new MatrixPoint(0, 0);
            this.CopyTo(matrix, new MatrixRectangle(new MatrixPoint(1, 1), size), leftTopCorner, MatrixStatus.NoMask);

            MatrixPoint rightTopCorner = new MatrixPoint(matrix.Width - this.Width + 1, 0);
            this.CopyTo(matrix, new MatrixRectangle(new MatrixPoint(0, 1), size), rightTopCorner, MatrixStatus.NoMask);


            MatrixPoint leftBottomCorner = new MatrixPoint(0, matrix.Width - this.Width + 1);
            this.CopyTo(matrix, new MatrixRectangle(new MatrixPoint(1, 0), size), leftBottomCorner, MatrixStatus.NoMask);
        }

        private MatrixSize GetSizeOfSquareWithSeparators()
        {
            return new MatrixSize(this.Width - 1, this.Height - 1);
        }
    }
}
