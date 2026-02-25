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
			Add("1", false, "1");
			Add("1.", false, null);
			Add("1.0", false, "1.0");
			Add("", false, null);
			Add("0.0", false, "0.0");
			Add("0", false, "0");
			Add("0.", false, null);
			Add(".1", true, "0.1");
			Add(".", false, null);
			Add(",", false, null);
			Add("20999999", false, "20999999");
			Add("2.1", false, "2.1");
			Add("1.11111111", false, "1.11111111");
			Add("1.00000001", false, "1.00000001");
			Add("20999999.9769", false, "20999999.9769");
			Add(" ", true, "");
			Add("  ", true, "");
			Add("abc", false, null);
			Add("1a", false, null);
			Add("a1a", false, null);
			Add("a1 a", false, null);
			Add("a2 1 a", false, null);
			Add("2,1", true, "2.1");
			Add("2٫1", true, "2.1");
			Add("2٬1", true, "2.1");
			Add("2⎖1", true, "2.1");
			Add("2·1", true, "2.1");
			Add("2'1", true, "2.1");
			Add("2.1.", false, null);
			Add("2.1..", false, null);
			Add("2.1.,.", false, null);
			Add("2.1. . .", false, null);
			Add("2.1.1", false, null);
			Add("2,1.1", false, null);
			Add(".1.", false, null);
			Add(",1", true, "0.1");
			Add("..1", false, null);
			Add(".,1", false, null);
			Add("01", true, "1");
			Add("001", true, "1");
			Add("001.0", true, "1.0");
			Add("001.00", true, "1.00");
			Add("00", true, "0");
			Add("0  0", true, "0");
			Add("001.", false, null);
			Add("00....1...,a", false, null);
			Add("0...1.", false, null);
			Add("1...1.", false, null);
			Add("1.s.1...1.", false, null);

			// Negative values.
			Add("-0", true, "0");
			Add("-1", true, "1");
			Add("-0.5", true, "0.5");
			Add("-0,5", true, "0.5");

			// Invalid values.
			Add("tb1qzvp8n2k2v3k2v3k2v3k2v3k2v3k2v3k2v3k2v", false, null);
		}
	}

	private class BitcoinTestData : CurrencyTestData
	{
		public BitcoinTestData()
		{
			Add("1.000000000", true, "1.00000000");
			Add("1.111111119", true, "1.11111111");
			Add("20999999.97690001", false, "20999999.97690001");
			Add("30999999", false, "30999999");
			Add("303333333333333333999999", false, "303333333333333333999999");
			Add("20999999.977", false, "20999999.977");
			Add("209999990.9769", false, "209999990.9769");
			Add("20999999.976910000000000", true, "20999999.97691000");
			Add("209999990000000000.97000000000069", true, "209999990000000000.97000000");
			Add("1.000000001", true, "1.00000000");
			Add("20999999.97000000000069", true, "20999999.97000000");
		}
	}
}
