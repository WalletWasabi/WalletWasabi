using NBitcoin;
using WalletWasabi.Userfacing.Bip321;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing.Bip321;

/// <summary>
/// Tests for <see cref="Bip321UriParser"/>
/// </summary>
public class Bip321UriParserTests
{
	[Fact]
	public void TryParseTests()
	{
		Assert.False(Bip321UriParser.TryParse(input: "", Network.Main, out Bip321UriParser.Result? result, out Bip321UriParser.Error? error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidUri, error);

		Assert.False(Bip321UriParser.TryParse(input: "nfdjksnfjkdsnfjkds", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidUri, error);

		Assert.False(Bip321UriParser.TryParse("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidUri, error);

		Assert.False(Bip321UriParser.TryParse("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidUri, error);

		Assert.False(Bip321UriParser.TryParse("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidUri, error);

		Assert.False(Bip321UriParser.TryParse("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=20.3", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidUri, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorMissingAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:?amount=20.3", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorMissingAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorMissingAmountValue, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=XYZ", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAmountValue, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100'000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAmountValue, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100,000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAmountValue, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=XYZ", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=100'000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=100000", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorUnsupportedReqParameter, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorInvalidAddress, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=1&amount=2", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorDuplicateParameter, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?label=a&Label=b", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorDuplicateParameter, error);

		Assert.False(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?message=a&Message=b", Network.Main, out result, out error));
		Assert.Null(result);
		AssertEqualErrors(Bip321UriParser.ErrorDuplicateParameter, error);

		// Success cases.
		Assert.True(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		Assert.True(Bip321UriParser.TryParse("BITCOIN:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		Assert.True(Bip321UriParser.TryParse("BitCoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		// Silent payment with no fallback.
		Assert.True(Bip321UriParser.TryParse("bitcoin:?sp=sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", result.Address.ToWif(Network.Main));

		Assert.True(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?label=Luke-Jr", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);

		Assert.True(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3&label=Luke-Jr", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(20.3m), result.Amount);

		// As per BIP321, query keys are case insensitive, "amount", "Amount", "label", and "Label" are all valid.
		Assert.True(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?Amount=20.3&Label=Luke-Jr", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(20.3m), result.Amount);

		// From BIP321: "Many QR codes utilize all-uppercase URIs, which should be handled fine"
		Assert.True(Bip321UriParser.TryParse("BITCOIN:BC1QUFGY354J3KMVUCH987XE4S40836X3H0LG8F5N2", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("bc1qufgy354j3kmvuch987xe4s40836x3h0lg8f5n2", result.Address.ToWif(Network.Main));
		Assert.Null(result.Label);
		Assert.Null(result.Amount);

		Assert.True(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip321UriParser.TryParse("bitcoin:3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip321UriParser.TryParse("bitcoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip321UriParser.TryParse("bitcoin:2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet, out result, out error));
		Assert.Null(error);
		Assert.Equal("2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip321UriParser.TryParse("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet, out result, out error));
		Assert.Null(error);
		Assert.Equal("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(Bip321UriParser.TryParse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?somethingyoudontunderstand=50&somethingelseyoudontget=999", Network.Main, out result, out error));
		Assert.Null(error);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		Assert.True(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=0.02&label=bolt11_example&lightning=lntb20m1pvjluezsp5zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zygshp58yjmdan79s6qqdhdzgynm4zwqd5d7xmw5fk98klysy043l2ahrqspp5qqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqypqfpp3x9et2e20v6pu37c5d9vax37wxq72un989qrsgqdj545axuxtnfemtpwkc45hx9d2ft7x04mt8q7y6t0k2dge9e7h8kpy9p34ytyslj3yu569aalz2xdk8xkd7ltxqld94u8h2esmsmacgpghe9k8", Network.TestNet, out result, out error));
		Assert.Null(error);
		Assert.Equal("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", result.Address.ToWif(Network.Main));
		Assert.Equal("bolt11_example", result.Label);
		Assert.Equal(Money.Coins(0.02m), result.Amount);

		// Handling of unknown parameters.
		{
			Assert.True(Bip321UriParser.TryParse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=0.02&label=unknown_params&unknown1=1&unknown2=true&unknown3=someValue", Network.TestNet, out result, out error));
			Assert.Null(error);
			Assert.Equal("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", result.Address.ToWif(Network.Main));
			Assert.Equal("unknown_params", result.Label);
			Assert.Equal(Money.Coins(0.02m), result.Amount);
			Assert.True(result.UnknownParameters.TryGetValue("unknown1", out var unknown1));
			Assert.Equal("1", unknown1);
			Assert.True(result.UnknownParameters.TryGetValue("unknown2", out var unknown2));
			Assert.Equal("true", unknown2);
			Assert.True(result.UnknownParameters.TryGetValue("unknown3", out var unknown3));
			Assert.Equal("someValue", unknown3);
		}

		// Fallback addresses.
		{
			{
				// Request funds to be paid over lightning to a BOLT 11 invoice with a fallback to on-chain payments (i.e. bc1qp6ejw8ptj9l9pkscmlf8fhhkrrjeawgpyjvtq8).
				// Lightning is not supported by Wasabi, so we will just parse the fallback address and ignore the lightning parameter.
				Assert.True(Bip321UriParser.TryParse("bitcoin:bc1qp6ejw8ptj9l9pkscmlf8fhhkrrjeawgpyjvtq8?lightning=lnbc420bogusinvoice", Network.Main, out result, out error));
				Assert.Null(error);
				Assert.Equal("bc1qp6ejw8ptj9l9pkscmlf8fhhkrrjeawgpyjvtq8", result.Address.ToWif(Network.Main));
				Assert.Null(result.Label);
				Assert.Null(result.Amount);
				Assert.True(result.UnknownParameters.TryGetValue("lightning", out var unknown1));
				Assert.Equal("lnbc420bogusinvoice", unknown1);
			}

			{
				// Silent payment with a fallback. Fallback address must be ignored in this case.
				Assert.True(Bip321UriParser.TryParse("bitcoin:bc1qp6ejw8ptj9l9pkscmlf8fhhkrrjeawgpyjvtq8?sp=sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", Network.Main, out result, out error));
				Assert.Null(error);
				Assert.Equal("sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", result.Address.ToWif(Network.Main));
				Assert.Null(result.Label);
				Assert.Null(result.Amount);
				Assert.Empty(result.UnknownParameters);
			}
		}
	}

	/// <summary>
	/// Helper method that does not compare <see cref="Bip321UriParser.Error.Details"/> property.
	/// </summary>
	private void AssertEqualErrors(Bip321UriParser.Error expected, Bip321UriParser.Error? actual)
	{
		Assert.NotNull(actual);
		Assert.Equal(expected.Code, actual.Code);
		Assert.Equal(expected.Message, actual.Message);
	}
}
