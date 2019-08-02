using System;

namespace Gma.QrCodeNet.Encoding.Positioning.Stencils
{
	internal abstract class PatternStencilBase : BitMatrix
	{
		public int Version { get; private set; }

		internal PatternStencilBase(int version)
		{
			Version = version;
		}

		protected const bool O = false;
		protected const bool X = true;

		public abstract bool[,] Stencil { get; }

		public override bool this[int i, int j]
		{
			get => Stencil[i, j];
			set => throw new NotSupportedException();
		}

		public override int Width => Stencil.GetLength(0);

		public override int Height => Stencil.GetLength(1);

		public override bool[,] InternalArray => throw new NotImplementedException();

		public abstract void ApplyTo(TriStateMatrix matrix);
	}
}
