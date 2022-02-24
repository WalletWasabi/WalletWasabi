using Moq;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
		Assert.True(man.WalletCoinJoinState is Stopped);

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is Stopped);

		man.Stop();

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is Stopped);

		man.Play();

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is Playing wait && !wait.IsInRound && !wait.InCriticalPhase);

		man.Stop();

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is Stopped);
	}

	[Fact]
	public void AutoCoinJoinOnTest()
	{
		KeyManager keyManager = ServiceFactory.CreateKeyManager();
		keyManager.AutoCoinJoin = true;

		using Wallets.Wallet wallet = new(Common.DataDir, keyManager.GetNetwork(), keyManager);

		WalletCoinJoinManager man = new(wallet);

		Assert.True(man.WalletCoinJoinState is Stopped);

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is AutoStarting);
		if (man.WalletCoinJoinState is AutoStarting s1)
		{
			Assert.False(s1.IsSending);
			Assert.False(s1.IsDelay);
			Assert.False(s1.IsPaused);
			Assert.False(s1.IsPlebStop);
		}

		WalletCoinJoinManager.IsUserInSendWorkflow = true;
		man.Pause();

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is AutoStarting);
		if (man.WalletCoinJoinState is AutoStarting s2)
		{
			Assert.True(s2.IsSending);
			Assert.True(s2.IsDelay);
			Assert.True(s2.IsPaused);
			Assert.True(s2.IsPlebStop);
		}

		WalletCoinJoinManager.IsUserInSendWorkflow = false;
		man.Play();

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is AutoStarting);
		if (man.WalletCoinJoinState is AutoStarting s3)
		{
			Assert.False(s3.IsSending);
			Assert.False(s3.IsDelay);
			Assert.False(s3.IsPaused);
			Assert.True(s3.IsPlebStop);
		}

		keyManager.PlebStopThreshold = Money.Coins(-1m);

		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is Playing);

		man.Stop();
		man.UpdateState();
		Assert.True(man.WalletCoinJoinState is Stopped);
	}
}
