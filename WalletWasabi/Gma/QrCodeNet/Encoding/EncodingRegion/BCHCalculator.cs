namespace Gma.QrCodeNet.Encoding.EncodingRegion;

internal static class BCHCalculator
{
	/// <summary>
	/// Calculate int length by search for Most significant bit
	/// </summary>
	/// <param name="num">Input Number</param>
	/// <returns>Most significant bit</returns>
	internal static int PosMSB(int num) => num == 0 ? 0 : BinarySearchPos(num, 0, 32) + 1;

	/// <summary>
	/// Search for right side bit of Most significant bit
	/// </summary>
	/// <param name="num">Input number</param>
	/// <param name="lowBoundary">Lower boundary. At start should be 0</param>
	/// <param name="highBoundary">Higher boundary. At start should be 32</param>
	/// <returns>Most significant bit - 1</returns>
	private static int BinarySearchPos(int num, int lowBoundary, int highBoundary)
	{
		int mid = (lowBoundary + highBoundary) / 2;
		int shiftResult = num >> mid;
		if (shiftResult == 1)
		{
			return mid;
		}
		else if (shiftResult < 1)
		{
			return BinarySearchPos(num, lowBoundary, mid);
		}
		else
		{
			return BinarySearchPos(num, mid, highBoundary);
		}
	}

	/// <summary>
	/// With input number and polynomial number. Method will calculate BCH value and return
	/// </summary>
	/// <param name="num">Input number</param>
	/// <param name="poly">Polynomial number</param>
	/// <returns>BCH value</returns>
	internal static int CalculateBCH(int num, int poly)
	{
		int polyMSB = PosMSB(poly);

		// num's length will be old length + new length - 1.
		// Once divide poly number. BCH number will be one length short than Poly number's length.
		num <<= (polyMSB - 1);
		int numMSB = PosMSB(num);
		while (PosMSB(num) >= polyMSB)
		{
			// left shift Poly number to same level as num. Then xor.
			// Remove most significant bits of num.
			num ^= poly << (numMSB - polyMSB);
			numMSB = PosMSB(num);
		}
		return num;
	}
}
