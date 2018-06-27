using System;
using System.Linq;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.Positioning.Stencils
{
    internal class AlignmentPattern : PatternStencilBase
    {
        private static readonly bool[,] s_AlignmentPattern = new[,]
                                                                          {
                                                                              { x, x, x, x, x }, 
                                                                              { x, o, o, o, x }, 
                                                                              { x, o, x, o, x }, 
                                                                              { x, o, o, o, x }, 
                                                                              { x, x, x, x, x }
                                                                          };

        public AlignmentPattern(int version) 
            : base(version)
        {
        }

        public override bool[,] Stencil
        {
            get { return s_AlignmentPattern; }
        }

        public override void ApplyTo(TriStateMatrix matrix)
        {
            foreach (MatrixPoint coordinatePair in GetNonColidingCoordinatePairs(matrix))
            {
                this.CopyTo(matrix, coordinatePair, MatrixStatus.NoMask);
            }
        }

        public IEnumerable<MatrixPoint> GetNonColidingCoordinatePairs(TriStateMatrix matrix)
        {
            return
                GetAllCoordinatePairs()
                    .Where(point => matrix.MStatus(point.Offset(2, 2)) == MatrixStatus.None);
        }

        private IEnumerable<MatrixPoint> GetAllCoordinatePairs()
        {
            IEnumerable<byte> coordinates = GetPatternCoordinatesByVersion(Version);
            foreach (byte centerX in coordinates)
            {
                foreach (byte centerY in coordinates)
                {
                    MatrixPoint location = new MatrixPoint(centerX - 2, centerY - 2);
                    yield return location;
                }
            }
        }

        private static IEnumerable<byte> GetPatternCoordinatesByVersion(int version)
        {
            return s_AlignmentPatternCoordinatesByVersion[version];
        }

        //Table E.1 — Row/column coordinates of center module of Alignment Patterns
        private static readonly byte[][] s_AlignmentPatternCoordinatesByVersion = new[]
                                                                                         {
                                                                                             null,
                                                                                             new byte[] {} , 
                                                                                             new byte[] { 6, 18}, 
                                                                                             new byte[] { 6, 22 }, 
                                                                                             new byte[] { 6, 26 }, 
                                                                                             new byte[] { 6, 30 }, 
                                                                                             new byte[] { 6, 34 }, 
                                                                                             new byte[] { 6, 22, 38 }, 
                                                                                             new byte[] { 6, 24, 42 }, 
                                                                                             new byte[] { 6, 26, 46 }, 
                                                                                             new byte[] { 6, 28, 50 }, 
                                                                                             new byte[] { 6, 30, 54 }, 
                                                                                             new byte[] { 6, 32, 58 }, 
                                                                                             new byte[] { 6, 34, 62 }, 
                                                                                             new byte[] { 6, 26, 46, 66 }, 
                                                                                             new byte[] { 6, 26, 48, 70 }, 
                                                                                             new byte[] { 6, 26, 50, 74 }, 
                                                                                             new byte[] { 6, 30, 54, 78 }, 
                                                                                             new byte[] { 6, 30, 56, 82 }, 
                                                                                             new byte[] { 6, 30, 58, 86 }, 
                                                                                             new byte[] { 6, 34, 62, 90 }, 
                                                                                             new byte[] { 6, 28, 50, 72, 94 }, 
                                                                                             new byte[] { 6, 26, 50, 74, 98 }, 
                                                                                             new byte[] { 6, 30, 54, 78, 102 }, 
                                                                                             new byte[] { 6, 28, 54, 80, 106 }, 
                                                                                             new byte[] { 6, 32, 58, 84, 110 }, 
                                                                                             new byte[] { 6, 30, 58, 86, 114 }, 
                                                                                             new byte[] { 6, 34, 62, 90, 118 }, 
                                                                                             new byte[] { 6, 26, 50, 74, 98, 122 }, 
                                                                                             new byte[] { 6, 30, 54, 78, 102, 126 }, 
                                                                                             new byte[] { 6, 26, 52, 78, 104, 130 }, 
                                                                                             new byte[] { 6, 30, 56, 82, 108, 134 }, 
                                                                                             new byte[] { 6, 34, 60, 86, 112, 138 }, 
                                                                                             new byte[] { 6, 30, 58, 86, 114, 142 }, 
                                                                                             new byte[] { 6, 34, 62, 90, 118, 146 }, 
                                                                                             new byte[] { 6, 30, 54, 78, 102, 126, 150 }, 
                                                                                             new byte[] { 6, 24, 50, 76, 102, 128, 154 }, 
                                                                                             new byte[] { 6, 28, 54, 80, 106, 132, 158 }, 
                                                                                             new byte[] { 6, 32, 58, 84, 110, 136, 162 }, 
                                                                                             new byte[] { 6, 26, 54, 82, 110, 138, 166 }, 
                                                                                             new byte[] { 6, 30, 58, 86, 114, 142, 170 }
                                                                                         };

    }
}
