using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class Pattern2 : Pattern
{
        public override bool this[int i, int j]
        {
            get { return i % 3 == 0; }
            set { throw new NotSupportedException(); }
        }

        public override MaskPatternType MaskPatternType
        {
            get { return MaskPatternType.Type2; }
        }
}
}
