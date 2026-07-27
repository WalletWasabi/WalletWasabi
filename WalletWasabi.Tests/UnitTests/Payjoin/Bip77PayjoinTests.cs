using NBitcoin;
using WalletWasabi.Payjoin;
using WalletWasabi.Userfacing;
using Xunit;
using Uri = System.Uri;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// Tests for the BIP 77 (async payjoin) URI layer: recognizing a <c>pj=</c> endpoint and
/// preserving the fragment params (and <c>pjos</c>) through BIP 21 parsing so the sender can
/// feed a faithful URI to payjoin-ffi.
/// </summary>
public class Bip77PayjoinTests
{
	/// <summary>
	/// A realistic BIP 77 <c>pj=</c> value: a directory URL whose fragment carries the receiver's
	/// ephemeral parameters as uppercase bech32, '+'-delimited (<c>EX1…</c> expiry, <c>OH1…</c>
	/// OHTTP keys, <c>RK1…</c> receiver key) — the shape rust-payjoin emits (core/uri/v2.rs).
	/// </summary>
	private const string Bip77PjUrl =
		"https://payjo.in/AbCd1234#EX1C4UC6ES+OH1QYPM5JXYNS754Y4R45QWE336QFYGGBW02VQHPARPAR4MBM8FMCLJPRE+RK1Q0DJS3VVDXWQQTLQ8022QGXSX7ML9PHZ6EDSF6AKEWQG758JPS2EV";

	/// <summary>
	/// Inside a BIP 21 URI the whole pj value is percent-encoded; <see cref="AddressParser"/> must
	/// hand back the decoded URL, fragment intact, so the sender can feed it to payjoin-ffi.
	/// This pins the current parser behavior (HttpUtility.ParseQueryString percent-decodes).
	/// </summary>
	[Fact]
	public void AddressParser_PreservesBip77PjUrlFragmentParameters()
	{
		string encodedPjUrl = Uri.EscapeDataString(Bip77PjUrl);
		string bip21 = $"bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=0.00010727&pj={encodedPjUrl}";

		var result = AddressParser.Parse(bip21, Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Equal(Bip77PjUrl, uri.PayjoinEndpoint);
		Assert.Equal(0.00010727m, uri.Amount);
	}

	/// <summary>
	/// <c>pjos=0</c> (output substitution disabled) must survive parsing so the sender can
	/// rebuild a faithful BIP 21 for payjoin-ffi.
	/// </summary>
	[Fact]
	public void AddressParser_PreservesPjosParameter()
	{
		string bip21 = $"bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=1&pj={Uri.EscapeDataString(Bip77PjUrl)}&pjos=0";

		var result = AddressParser.Parse(bip21, Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Equal("0", uri.PayjoinOutputSubstitution);

		// And it round-trips through serialization.
		Assert.Contains("pjos=0", uri.ToWif(Network.TestNet));
	}

	[Fact]
	public void Bip77UriParams_DetectsVersionAndExtractsReceiverKey()
	{
		Assert.True(Bip77UriParams.IsBip77(Bip77PjUrl));
		Assert.True(Bip77UriParams.TryGetReceiverKey(Bip77PjUrl, out var receiverKey));
		Assert.Equal("RK1Q0DJS3VVDXWQQTLQ8022QGXSX7ML9PHZ6EDSF6AKEWQG758JPS2EV", receiverKey);

		// A BIP 78 (v1) endpoint has no BIP 77 fragment params.
		Assert.False(Bip77UriParams.IsBip77("https://btcpay.example/BTC/pj"));
		Assert.False(Bip77UriParams.TryGetReceiverKey("https://btcpay.example/BTC/pj", out _));

		// A fragment without the receiver key is not BIP 77.
		Assert.False(Bip77UriParams.IsBip77("https://payjo.in/AbCd1234#EX1C4UC6ES"));
	}

	/// <summary>BIP 21 without a pj parameter yields no payjoin endpoint.</summary>
	[Fact]
	public void AddressParser_NoPjParameter_YieldsNullEndpoint()
	{
		var result = AddressParser.Parse("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=1", Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Null(uri.PayjoinEndpoint);
	}
}
