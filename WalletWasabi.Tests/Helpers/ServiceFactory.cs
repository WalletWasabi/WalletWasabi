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
		Func<FeeRate>? feeRateFetcher = null,
		bool allowUnconfirmed = true,
		bool watchOnly = false,
		bool allowDoubleSpend = false,
		int[]? allowedInputsIndexes = null,
		bool tryToSign = true)
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

		var sCoins = coins.Select(x => BitcoinFactory.CreateSmartCoin(keys[x.KeyIndex], x.Amount, x.Confirmed, x.AnonymitySet)).ToArray();
		foreach (var coin in sCoins)
		{
			foreach (var sameLabelCoin in sCoins.Where(c => !c.HdPubKey.Labels.IsEmpty && c.HdPubKey.Labels == coin.HdPubKey.Labels))
			{
				sameLabelCoin.HdPubKey.Cluster = coin.HdPubKey.Cluster;
			}
		}

		var uniqueCoins = sCoins.Distinct().Count();
		if (uniqueCoins != sCoins.Length)
		{
			throw new InvalidOperationException($"Coin clones have been detected. Number of all coins:{sCoins.Length}, unique coins:{uniqueCoins}.");
		}

		IEnumerable<SmartCoin>? allowedInputs = null;
		if (allowedInputsIndexes is not null)
		{
			allowedInputs = new List<SmartCoin>();
			foreach (var i in allowedInputsIndexes)
			{
				allowedInputs = allowedInputs.Append(sCoins[i]);
			}
		}

		var coinsView = new CoinsView(sCoins);
		var mockTransactionStore = new AllTransactionStore(".", Network.Main);

		feeRateFetcher ??= () => new FeeRate(2m);

		TransactionFactoryParameters parameters = new(FeeRateFetcher: feeRateFetcher, AllowUnconfirmed: allowUnconfirmed, AllowDoubleSpend: allowDoubleSpend, AllowedInputs: allowedInputs?.Select(c => c.Outpoint), TryToSign: tryToSign);
		return new TransactionFactory(Network.Main, keyManager, coinsView, mockTransactionStore, parameters, password);
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
