using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
	internal class Pattern7 : Pattern
	{
		public override bool this[int i, int j]
		{
			get => (((i * j) % 3) + (((i + j) % 2) % 2)) == 0;
			set => throw new NotSupportedException();
		}

		public override MaskPatternType MaskPatternType => MaskPatternType.Type7;
	}
}
