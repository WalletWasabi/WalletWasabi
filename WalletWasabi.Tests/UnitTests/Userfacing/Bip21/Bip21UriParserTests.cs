using NBitcoin;
using WalletWasabi.Userfacing.Bip21;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing.Bip21;

/// <summary>
/// Tests for <see cref="Bip21UriParser"/>
/// </summary>
public class Bip21UriParserTests
{
	[Fact]
	public void TryParseTests()
	{
		Assert.False(Bip21UriParser.TryParse(input: "", Network.Main, out Bip21UriParser.Result? result, out Bip21UriParser.Error? error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidUri, error);

		Assert.False(Bip21UriParser.TryParse(input: "nfdjksnfjkdsnfjkds", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidUri, error);

		Assert.False(Bip21UriParser.TryParse("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidUri, error);

		Assert.False(Bip21UriParser.TryParse("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidUri, error);

		Assert.False(Bip21UriParser.TryParse("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidUri, error);

		Assert.False(Bip21UriParser.TryParse("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=20.3", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidUri, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorMissingAddress, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:?amount=20.3", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorMissingAddress, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorMissingAmountValue, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=XYZ", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAmountValue, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100'000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAmountValue, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100,000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAmountValue, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=XYZ", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=100'000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=100000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorUnsupportedReqParameter, error);

		Assert.False(Bip21UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip21UriParser.ErrorInvalidAddress, error);

		// Success cases.
		Assert.True(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());

		Assert.True(Bip21UriParser.TryParse("BITCOIN:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());

		Assert.True(Bip21UriParser.TryParse("BitCoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());

		Assert.True(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?label=Luke-Jr", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);

		Assert.True(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3&label=Luke-Jr", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(20.3m), result.Amount);

		// As per BIP21, keys are case sensitive, "Amount" and "Label" is not valid, only "amount" and "label" is.
		Assert.True(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?Amount=20.3&Label=Luke-Jr", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());
		Assert.Equal(null!, result.Label);
		Assert.Equal(null!, result.Amount);

		Assert.True(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip21UriParser.TryParse("bitcoin:3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip21UriParser.TryParse("bitcoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip21UriParser.TryParse("bitcoin:2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet, out result, out error));
		Assert.Null(error);
		Assert.Equal("2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip21UriParser.TryParse("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet, out result, out error));
		Assert.Null(error);
		Assert.Equal("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", result.Address.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip21UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?somethingyoudontunderstand=50&somethingelseyoudontget=999", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToString());

		Assert.True(Bip21UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=0.02&label=bolt11_example&lightning=lntb20m1pvjluezsp5zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zygshp58yjmdan79s6qqdhdzgynm4zwqd5d7xmw5fk98klysy043l2ahrqspp5qqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqypqfpp3x9et2e20v6pu37c5d9vax37wxq72un989qrsgqdj545axuxtnfemtpwkc45hx9d2ft7x04mt8q7y6t0k2dge9e7h8kpy9p34ytyslj3yu569aalz2xdk8xkd7ltxqld94u8h2esmsmacgpghe9k8", Network.TestNet, out result, out error));
		Assert.Null(error);
		Assert.Equal("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", result.Address.ToString());
		Assert.Equal("bolt11_example", result.Label);
		Assert.Equal(Money.Coins(0.02m), result.Amount);
	}

	/// <summary>
	/// Helper method that does not compare <see cref="Bip21UriParser.Error.Details"/> property.
	/// </summary>
	private void AssertEqualErrors(Bip21UriParser.Error expected, Bip21UriParser.Error? actual)
	{
		Assert.NotNull(actual);
		Assert.Equal(expected.Code, actual.Code);
		Assert.Equal(expected.Message, actual.Message);
	}
}
