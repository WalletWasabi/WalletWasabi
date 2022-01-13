namespace Gma.QrCodeNet.Encoding;

public struct MatrixSize
{
	internal MatrixSize(int width, int height)
		: this()
	{
		Width = width;
		Height = height;
	}

	public int Width { get; private set; }
	public int Height { get; private set; }

	public override string ToString()
	{
		return $"Size({Width};{Height})";
	}
}
