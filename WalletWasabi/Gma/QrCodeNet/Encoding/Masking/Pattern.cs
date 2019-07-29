using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
	public abstract class Pattern : BitMatrix
	{
		public override int Width => throw new NotSupportedException();
		public override int Height => throw new NotSupportedException();

		public override bool[,] InternalArray => throw new NotImplementedException();

		public abstract MaskPatternType MaskPatternType { get; }
	}
}
