using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class Pattern3 : Pattern
{
        public override bool this[int i, int j]
        {
            get { return (j + i) % 3 == 0; }
            set { throw new NotSupportedException(); }
        }

        public override MaskPatternType MaskPatternType
        {
            get { return MaskPatternType.Type3; }
        }
}
}
