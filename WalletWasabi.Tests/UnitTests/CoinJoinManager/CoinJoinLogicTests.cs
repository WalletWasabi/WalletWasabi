using Xunit;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager.Logic;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.Wallets;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

namespace WalletWasabi.Tests.UnitTests.CoinJoinManager;

public class CoinJoinLogicTests
{
	[Fact]
	public void CanStartCoinJoinTest()
	{
		Assert.Equal(
			CoinjoinError.NotEnoughUnprivateBalance,
			Assert.Throws<CoinJoinClientException>(() => CoinJoinLogic.AssertStartCoinJoin(
				walletBlockedByUi: false,
				wallet: new MockWallet(isUnderPlebStop: true),
				overridePlebStop: false,
				wasabiBackendStatusProvider: new MockWasabiBackendStatusProvider(setNullLastResponse: false))).CoinjoinError);

		Assert.Equal(
			CoinjoinError.BackendNotSynchronized,
			Assert.Throws<CoinJoinClientException>(() => CoinJoinLogic.AssertStartCoinJoin(walletBlockedByUi: false, new MockWallet(false), true, new MockWasabiBackendStatusProvider(true))).CoinjoinError);

		Assert.Equal(
			CoinjoinError.UserInSendWorkflow,
			Assert.Throws<CoinJoinClientException>(() => CoinJoinLogic.AssertStartCoinJoin(walletBlockedByUi: true, new MockWallet(true), true, new MockWasabiBackendStatusProvider(false))).CoinjoinError);
	}
}
