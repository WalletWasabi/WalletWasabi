namespace Gma.QrCodeNet.Encoding.ReedSolomon;

internal struct PolyDivideStruct
{
	internal PolyDivideStruct(Polynomial quotient, Polynomial remainder)
		: this()
	{
		Quotient = quotient;
		Remainder = remainder;
	}

	internal Polynomial Quotient { get; private set; }

	internal Polynomial Remainder { get; private set; }
}
