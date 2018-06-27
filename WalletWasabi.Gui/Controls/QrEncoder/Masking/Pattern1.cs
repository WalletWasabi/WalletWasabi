using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class Pattern1 : Pattern
    {
        public override bool this[int i, int j]
        {
            get { return j % 2 == 0; }
            set { throw new NotSupportedException(); }
        }

        public override MaskPatternType MaskPatternType
        {
            get { return MaskPatternType.Type1; }
        }
    }
}
