namespace Gma.QrCodeNet.Encoding.Positioning.Stencils;

internal abstract class PatternStencilBase : BitMatrix
{
	protected const bool O = false;
	protected const bool X = true;

	internal PatternStencilBase(int version)
	{
		Version = version;
	}

	public int Version { get; private set; }

	public abstract bool[,] Stencil { get; }

	public override int Width => Stencil.GetLength(0);

	public override int Height => Stencil.GetLength(1);

	public override bool[,] InternalArray => throw new NotImplementedException();

	public override bool this[int i, int j]
	{
		get => Stencil[i, j];
		set => throw new NotSupportedException();
	}

	public abstract void ApplyTo(TriStateMatrix matrix);
}
