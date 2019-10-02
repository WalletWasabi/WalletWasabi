using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.Masking
{
	internal class PatternFactory
	{
		internal Pattern CreateByType(MaskPatternType maskPatternType)
		{
			return maskPatternType switch
			{
				MaskPatternType.Type0 => new Pattern0(),
				MaskPatternType.Type1 => new Pattern1(),
				MaskPatternType.Type2 => new Pattern2(),
				MaskPatternType.Type3 => new Pattern3(),
				MaskPatternType.Type4 => new Pattern4(),
				MaskPatternType.Type5 => new Pattern5(),
				MaskPatternType.Type6 => new Pattern6(),
				MaskPatternType.Type7 => new Pattern7(),
				_ => throw new NotSupportedException("This is impossible.")
			};
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
