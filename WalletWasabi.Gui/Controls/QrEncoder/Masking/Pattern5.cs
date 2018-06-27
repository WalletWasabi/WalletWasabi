using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class Pattern5 : Pattern
{
        public override bool this[int i, int j]
        {
            get { return (i * j) % 2 + (i * j) % 3 == 0; }
            set { throw new NotSupportedException(); }
        }

        public override MaskPatternType MaskPatternType
        {
            get { return MaskPatternType.Type5; }
        }
}
}
