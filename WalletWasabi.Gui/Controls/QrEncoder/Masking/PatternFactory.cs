using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gma.QrCodeNet.Encoding.Masking
{
    internal class PatternFactory
    {
        internal Pattern CreateByType(MaskPatternType maskPatternType)
        {
            switch (maskPatternType)
            {
                case MaskPatternType.Type0:
                    return new Pattern0();

                case MaskPatternType.Type1:
                    return new Pattern1();

                case MaskPatternType.Type2:
                    return new Pattern2();

                case MaskPatternType.Type3:
                    return new Pattern3();

                case MaskPatternType.Type4:
                    return new Pattern4();

                case MaskPatternType.Type5:
                    return new Pattern5();

                case MaskPatternType.Type6:
                    return new Pattern6();

                case MaskPatternType.Type7:
                    return new Pattern7();
            }

            throw new ArgumentException(string.Format("Usupported pattern type {0}", maskPatternType), "maskPatternType");
        }

        internal IEnumerable<Pattern> AllPatterns()
        {
            foreach (MaskPatternType patternType in Enum.GetValues(typeof(MaskPatternType)))
            {
                yield return CreateByType(patternType);
            }
        }
    }
}
