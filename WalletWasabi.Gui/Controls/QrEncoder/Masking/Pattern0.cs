using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class Pattern0 : Pattern
    {
        public override bool this[int i, int j]
        {
            get { return (j + i) % 2 == 0; }
            set { throw new NotSupportedException(); }
        }

        public override MaskPatternType MaskPatternType
        {
            get { return MaskPatternType.Type0; }
        }
    }
}
