using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class Pattern7 : Pattern
    {
        public override bool this[int i, int j]
        {
            get { return ((i * j) % 3 + (i + j) % 2) % 2 == 0; }
            set { throw new NotSupportedException(); }
        }

        public override MaskPatternType MaskPatternType
        {
            get { return MaskPatternType.Type7; }
        }
    }
}
