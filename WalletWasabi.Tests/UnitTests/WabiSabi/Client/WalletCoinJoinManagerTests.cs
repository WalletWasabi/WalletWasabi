using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class WalletCoinJoinManagerTests
{
	[Fact]
	public void AutoCoinJoinOffTest()
	{
		KeyManager keyManager = ServiceFactory.CreateKeyManager();
		keyManager.AutoCoinJoin = false;

		using Wallets.Wallet wallet = new(Common.DataDir, keyManager.GetNetwork(), keyManager);
		WalletCoinJoinManager man = new(wallet);

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Stopped);

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Stopped);

		man.Stop();

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Stopped);

		man.Play();

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Playing && !man.WalletCoinjoinState.InRound && !man.WalletCoinjoinState.InCriticalPhase);

		man.Stop();

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Stopped);
	}

	[Fact]
	public void AutoCoinJoinOnTest()
	{
		KeyManager keyManager = ServiceFactory.CreateKeyManager();
		keyManager.AutoCoinJoin = true;

		using Wallets.Wallet wallet = new(Common.DataDir, keyManager.GetNetwork(), keyManager);

		WalletCoinJoinManager man = new(wallet);

		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.AutoStarting);

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.AutoStarting);
		var s1 = man.WalletCoinjoinState;
		if (s1.Status == WalletCoinjoinState.State.AutoStarting)
		{
			Assert.False(s1.IsSending);
			Assert.True(s1.IsDelay);
			Assert.False(s1.IsPaused);
			Assert.True(s1.IsPlebStop);
		}

		WalletCoinJoinManager.IsUserInSendWorkflow = true;
		man.Pause();

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.AutoStarting);
		var s2 = man.WalletCoinjoinState;
		if (s2.Status == WalletCoinjoinState.State.AutoStarting)
		{
			Assert.True(s2.IsSending);
			Assert.True(s2.IsDelay);
			Assert.True(s2.IsPaused);
			Assert.True(s2.IsPlebStop);
		}

		WalletCoinJoinManager.IsUserInSendWorkflow = false;
		man.Play();

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.AutoStarting);
		var s3 = man.WalletCoinjoinState;
		if (s3.Status == WalletCoinjoinState.State.AutoStarting)
		{
			Assert.False(s3.IsSending);
			Assert.False(s3.IsDelay);
			Assert.False(s3.IsPaused);
			Assert.True(s3.IsPlebStop);
		}

		keyManager.PlebStopThreshold = Money.Coins(-1m);

		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Playing);

		man.Stop();
		man.UpdateState();
		Assert.True(man.WalletCoinjoinState.Status == WalletCoinjoinState.State.Stopped);
	}
}
