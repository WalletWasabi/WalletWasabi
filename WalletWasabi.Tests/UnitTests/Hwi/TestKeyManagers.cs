using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Tests.UnitTests.Hwi;

/// <summary>Watch-only wallets as importing a Trezor produces them, from the "all all all" seed Trezor's own tests use.</summary>
internal static class TestKeyManagers
{
	public static ExtKey MasterKey => new Mnemonic("all all all all all all all all all all all all").DeriveExtKey();

	/// <summary>A segwit account, plus the SLIP-25 coinjoin account as the taproot account when asked for.</summary>
	public static KeyManager WatchOnlyHardwareWallet(bool withCoinJoinAccount)
	{
		var masterKey = MasterKey;
		var keyManager = KeyManager.CreateNewHardwareWalletWatchOnly(
			masterKey.Neuter().PubKey.GetHDFingerPrint(),
			masterKey.Derive(new KeyPath("84'/0'/0'")).Neuter(),
			null,
			null,
			null,
			Network.Main);
		if (withCoinJoinAccount)
		{
			var coinJoinAccountKeyPath = Slip25.GetCoinJoinAccountKeyPath(Network.Main);
			keyManager.SetCoinJoinAccount(coinJoinAccountKeyPath, masterKey.Derive(coinJoinAccountKeyPath).Neuter());
		}

		return keyManager;
	}
}
