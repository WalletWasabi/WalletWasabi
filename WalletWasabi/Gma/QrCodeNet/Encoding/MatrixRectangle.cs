using System.Collections;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding;

internal struct MatrixRectangle : IEnumerable<MatrixPoint>
{
	internal MatrixRectangle(MatrixPoint location, MatrixSize size) :
		this()
	{
		Location = location;
		Size = size;
	}

	public MatrixPoint Location { get; private set; }
	public MatrixSize Size { get; private set; }

	public IEnumerator<MatrixPoint> GetEnumerator()
	{
		for (int j = Location.Y; j < Location.Y + Size.Height; j++)
		{
			for (int i = Location.X; i < Location.X + Size.Width; i++)
			{
				yield return new MatrixPoint(i, j);
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public override string ToString() => $"Rectangle({Location.X};{Location.Y}):({Size.Width} x {Size.Height})";
}
