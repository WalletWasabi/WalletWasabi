using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.Positioning.Stencils
{
    internal abstract class PatternStencilBase : BitMatrix
    {
        public int Version { get; private set; }

        internal PatternStencilBase(int version)
        {
            Version = version;
        }

        protected const bool o = false;
        protected const bool x = true;

        public abstract bool[,] Stencil { get; }

        public override bool this[int i, int j]
        {
            get { return Stencil[i, j]; }
            set { throw new NotSupportedException(); }
        }

        public override int Width
        {
            get { return Stencil.GetLength(0); }
        }

        public override int Height
        {
            get { return Stencil.GetLength(1); }
        }

        public override bool[,] InternalArray
        {
            get { throw new NotImplementedException(); }
        }

        public abstract void ApplyTo(TriStateMatrix matrix);
    }
}