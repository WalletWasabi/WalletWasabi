using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for <see cref="HandoverPolicy"/>.
/// </summary>
public class HandoverPolicyTests
{
	private static readonly WalletId Source = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
	private static readonly WalletId Destination = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

	[Fact]
	public void MixingIntoSelfIsNotMixingToOtherWallet()
	{
		Assert.False(HandoverPolicy.IsMixingToOtherWallet(Source, Source));
	}

	[Fact]
	public void MixingIntoDifferentWalletIsMixingToOtherWallet()
	{
		Assert.True(HandoverPolicy.IsMixingToOtherWallet(Source, Destination));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void MixingIntoSelfNeverHandsOver(bool isSourceWalletPrivate)
	{
		Assert.False(HandoverPolicy.IsReadyForHandover(Source, Source, isSourceWalletPrivate));
	}

	[Fact]
	public void DoesNotHandOverBeforeTheWalletIsPrivate()
	{
		Assert.False(HandoverPolicy.IsReadyForHandover(Source, Destination, isSourceWalletPrivate: false));
	}

	[Fact]
	public void HandsOverOnceTheWalletIsPrivate()
	{
		Assert.True(HandoverPolicy.IsReadyForHandover(Source, Destination, isSourceWalletPrivate: true));
	}

	[Fact]
	public void ResolvesAPersistedDestinationThatIsLoaded()
	{
		var resolved = HandoverPolicy.ResolveDestinationWalletName("Source", "Destination", ["Source", "Destination"]);

		Assert.Equal("Destination", resolved);
	}

	[Fact]
	public void FallsBackToSelfWhenNoDestinationIsPersisted()
	{
		var resolved = HandoverPolicy.ResolveDestinationWalletName("Source", null, ["Source", "Destination"]);

		Assert.Equal("Source", resolved);
	}

	[Fact]
	public void FallsBackToSelfWhenTheDestinationIsNotLoaded()
	{
		// Renamed, deleted, or simply not loaded yet - wallets load in an arbitrary order.
		var resolved = HandoverPolicy.ResolveDestinationWalletName("Source", "Destination", ["Source"]);

		Assert.Equal("Source", resolved);
	}
}
