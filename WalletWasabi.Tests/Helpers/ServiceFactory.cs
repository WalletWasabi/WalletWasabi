using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Tests.Helpers;

public static class ServiceFactory
{
	public static TransactionFactory CreateTransactionFactory(
		IEnumerable<(string Label, int KeyIndex, decimal Amount, bool Confirmed, int AnonymitySet)> coins,
		bool allowUnconfirmed = true,
		bool watchOnly = false)
	{
		var password = "foo";
		var keyManager = watchOnly ? CreateWatchOnlyKeyManager() : CreateKeyManager(password);

		var coinArray = coins.ToArray();

		var generated = keyManager.GetKeys().Count();
		var toGenerate = coinArray.Length - generated;
		for (int i = 0; i < toGenerate; i++)
		{
			keyManager.GenerateNewKey("", KeyState.Clean, false);
		}

		var keys = keyManager.GetKeys().Take(coinArray.Length).ToArray();
		for (int i = 0; i < coinArray.Length; i++)
		{
			var c = coinArray[i];
			var k = keys[c.KeyIndex];
			k.SetLabel(c.Label);
			k.SetAnonymitySet(c.AnonymitySet);
		}

		var scoins = coins.Select(x => BitcoinFactory.CreateSmartCoin(keys[x.KeyIndex], x.Amount, x.Confirmed, x.AnonymitySet)).ToArray();
		foreach (var coin in scoins)
		{
			foreach (var sameLabelCoin in scoins.Where(c => !c.HdPubKey.Label.IsEmpty && c.HdPubKey.Label == coin.HdPubKey.Label))
			{
				sameLabelCoin.HdPubKey.Cluster = coin.HdPubKey.Cluster;
			}
		}

		var uniqueCoins = scoins.Distinct().Count();
		if (uniqueCoins != scoins.Length)
		{
			throw new InvalidOperationException($"Coin clones have been detected. Number of all coins:{scoins.Length}, unique coins:{uniqueCoins}.");
		}

		var coinsView = new CoinsView(scoins);
		var mockTransactionStore = new Mock<AllTransactionStore>(".", Network.Main);
		return new TransactionFactory(Network.Main, keyManager, coinsView, mockTransactionStore.Object, password, allowUnconfirmed);
	}

	public static KeyManager CreateKeyManager(string password = "blahblahblah", bool isTaprootAllowed = false)
	{
		var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
		ExtKey extKey = mnemonic.DeriveExtKey(password);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
		BlockchainState blockchainState = new(Network.Main);
		KeyPath segwitAccountKeyPath = KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.Segwit);
		ExtPubKey segwitExtPubKey = extKey.Derive(segwitAccountKeyPath).Neuter();

		ExtPubKey? taprootExtPubKey = null;
		if (isTaprootAllowed)
		{
			KeyPath taprootAccountKeyPath = KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.TaprootBIP86);
			taprootExtPubKey = extKey.Derive(taprootAccountKeyPath).Neuter();
		}

		return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, segwitExtPubKey, taprootExtPubKey, skipSynchronization: true, 21, blockchainState, null, segwitAccountKeyPath, null);
	}

	public static KeyManager CreateWatchOnlyKeyManager()
	{
		Mnemonic mnemonic = new(Wordlist.English, WordCount.Twelve);
		ExtKey extKey = mnemonic.DeriveExtKey();

		return KeyManager.CreateNewWatchOnly(
			extKey.Derive(KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.Segwit)).Neuter(),
			extKey.Derive(KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.TaprootBIP86)).Neuter());
	}
}
