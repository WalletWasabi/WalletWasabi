using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
	internal class Pattern1 : Pattern
	{
		public override bool this[int i, int j]
		{
			get => j % 2 == 0;
			set => throw new NotSupportedException();
		}

		public override MaskPatternType MaskPatternType => MaskPatternType.Type1;
	}
}
