using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

public class CurrencyInputTests
{
	[Theory]
	[ClassData(typeof(CurrencyTestData))]
	public void CorrectAmountText(string amount, bool expectedResult, string expectedCorrection)
	{
		var result = CurrencyInput.TryCorrectAmount(amount, out var correction);
		Assert.Equal(expectedCorrection, correction);
		Assert.Equal(expectedResult, result);
	}

	[Theory]
	[ClassData(typeof(BitcoinTestData))]
	public void CorrectBitcoinAmountText(string amount, bool expectedResult, string expectedCorrection)
	{
		var result = CurrencyInput.TryCorrectBitcoinAmount(amount, out var correction);
		Assert.Equal(expectedCorrection, correction);
		Assert.Equal(expectedResult, result);
	}

	private class CurrencyTestData : TheoryData<string, bool, string?>
	{
		public CurrencyTestData()
		{
			Add("1", false, null);
			Add("1.", false, null);
			Add("1.0", false, null);
			Add("", false, null);
			Add("0.0", false, null);
			Add("0", false, null);
			Add("0.", false, null);
			Add(".1", false, null);
			Add(".", false, null);
			Add(",", true, ".");
			Add("20999999", false, null);
			Add("2.1", false, null);
			Add("1.11111111", false, null);
			Add("1.00000001", false, null);
			Add("20999999.9769", false, null);
			Add(" ", true, "");
			Add("  ", true, "");
			Add("abc", true, "");
			Add("1a", true, "1");
			Add("a1a", true, "1");
			Add("a1 a", true, "1");
			Add("a2 1 a", true, "21");
			Add("2,1", true, "2.1");
			Add("2٫1", true, "2.1");
			Add("2٬1", true, "2.1");
			Add("2⎖1", true, "2.1");
			Add("2·1", true, "2.1");
			Add("2'1", true, "2.1");
			Add("2.1.", true, "2.1");
			Add("2.1..", true, "2.1");
			Add("2.1.,.", true, "2.1");
			Add("2.1. . .", true, "2.1");
			Add("2.1.1", true, "");
			Add("2,1.1", true, "");
			Add(".1.", true, ".1");
			Add(",1", true, ".1");
			Add("..1", true, ".1");
			Add(".,1", true, ".1");
			Add("01", true, "1");
			Add("001", true, "1");
			Add("001.0", true, "1.0");
			Add("001.00", true, "1.00");
			Add("00", true, "0");
			Add("0  0", true, "0");
			Add("001.", true, "1.");
			Add("00....1...,a", true, "");
			Add("0...1.", true, "");
			Add("1...1.", true, "");
			Add("1.s.1...1.", true, "");

			// Negative values.
			Add("-0", true, "0");
			Add("-1", true, "1");
			Add("-0.5", true, "0.5");
			Add("-0,5", true, "0.5");
		}
	}

	private class BitcoinTestData : CurrencyTestData
	{
		public BitcoinTestData()
		{
			Add("1.000000000", true, "1.00000000");
			Add("1.111111119", true, "1.11111111");
			Add("20999999.97690001", true, "20999999.9769");
			Add("30999999", true, "20999999.9769");
			Add("303333333333333333999999", true, "20999999.9769");
			Add("20999999.977", true, "20999999.9769");
			Add("209999990.9769", true, "20999999.9769");
			Add("20999999.976910000000000", true, "20999999.9769");
			Add("209999990000000000.97000000000069", true, "20999999.9769");
			Add("1.000000001", true, "1.00000000");
			Add("20999999.97000000000069", true, "20999999.97000000");
		}
	}
}
