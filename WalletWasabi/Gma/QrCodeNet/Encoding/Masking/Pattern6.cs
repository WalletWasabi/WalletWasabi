using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
	internal class Pattern6 : Pattern
	{
		public override bool this[int i, int j]
		{
			get => (((i * j % 2) + (i * j % 3)) % 2) == 0;
			set => throw new NotSupportedException();
		}

		public override MaskPatternType MaskPatternType => MaskPatternType.Type6;
	}
}
