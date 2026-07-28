using System;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

/// <summary>
/// Tests for <see cref="SendViewModel.ParseTo"/>.
/// </summary>
public class SendViewModelParseTests
{
	private static string NewAddress()
	{
		using Key key = new();
		return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString();
	}

	[Fact]
	public void Bip21UriKeepsPayjoinEndpoint()
	{
		var endpoint = "https://payjoin.example/pj";
		var bip21 = $"bitcoin:{NewAddress()}?amount=0.001&pj={Uri.EscapeDataString(endpoint)}";

		var parse = SendViewModel.ParseTo(bip21, Network.Main);

		Assert.True(parse.IsValid);
		Assert.True(parse.IsBip21);
		Assert.Equal(endpoint, parse.PayJoinEndPoint);
		Assert.Equal(0.001m, parse.Amount);
		Assert.True(parse.IsFixedAmount);
	}

	[Fact]
	public void BareAddressCarriesNoPayjoinEndpoint()
	{
		var parse = SendViewModel.ParseTo(NewAddress(), Network.Main);

		Assert.True(parse.IsValid);
		Assert.False(parse.IsBip21);
		Assert.Null(parse.PayJoinEndPoint);
	}

	[Fact]
	public void Bip21UriWithoutPjCarriesNoPayjoinEndpoint()
	{
		var bip21 = $"bitcoin:{NewAddress()}?amount=0.5";

		var parse = SendViewModel.ParseTo(bip21, Network.Main);

		Assert.True(parse.IsValid);
		Assert.True(parse.IsBip21);
		Assert.Null(parse.PayJoinEndPoint);
	}

	[Fact]
	public void InvalidTextIsNotValid()
	{
		var parse = SendViewModel.ParseTo("not an address", Network.Main);

		Assert.False(parse.IsValid);
		Assert.Null(parse.ParsedAddress);
		Assert.Null(parse.PayJoinEndPoint);
	}
}
