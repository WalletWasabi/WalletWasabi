using NBitcoin;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class SmartCoinTests
{
	[Fact]
	public void SmartCoinEquality()
	{
		var tx = Transaction.Parse("0100000000010176f521a178a4b394b647169a89b29bb0d95b6bce192fd686d533eb4ea98464a20000000000ffffffff02ed212d020000000016001442da650f25abde0fef57badd745df7346e3e7d46fbda6400000000001976a914bd2e4029ce7d6eca7d2c3779e8eac36c952afee488ac02483045022100ea43ccf95e1ac4e8b305c53761da7139dbf6ff164e137a6ce9c09e15f316c22902203957818bc505bbafc181052943d7ab1f3ae82c094bf749813e8f59108c6c268a012102e59e61f20c7789aa73faf5a92dc8c0424e538c635c55d64326d95059f0f8284200000000", Network.TestNet);
		var index = 0U;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpk1 = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, false);
		var hdpk2 = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, false);
		tx.Outputs[0].ScriptPubKey = hdpk1.P2wpkhScript;
		tx.Outputs[1].ScriptPubKey = hdpk2.P2wpkhScript;

		var height = Height.Mempool;
		var stx = new SmartTransaction(tx, height);

		var coin = new SmartCoin(stx, index, hdpk1);

		// If the txId or the index differ, equality should think it's a different coin.
		var differentCoin = new SmartCoin(stx, index + 1, hdpk2);

		// If the txId and the index are the same, equality should think it's the same coin.
		var sameCoin = new SmartCoin(stx, index, hdpk1);

		Assert.Equal(coin, sameCoin);
		Assert.NotEqual(coin, differentCoin);
	}

	[Fact]
	public void IsSufficientlyDistancedFromExternalKeys()
	{
		// 1. External, no inputs ours:
		var tx = Transaction.Parse("0100000000010176f521a178a4b394b647169a89b29bb0d95b6bce192fd686d533eb4ea98464a20000000000ffffffff02ed212d020000000016001442da650f25abde0fef57badd745df7346e3e7d46fbda6400000000001976a914bd2e4029ce7d6eca7d2c3779e8eac36c952afee488ac02483045022100ea43ccf95e1ac4e8b305c53761da7139dbf6ff164e137a6ce9c09e15f316c22902203957818bc505bbafc181052943d7ab1f3ae82c094bf749813e8f59108c6c268a012102e59e61f20c7789aa73faf5a92dc8c0424e538c635c55d64326d95059f0f8284200000000", Network.TestNet);
		var index = 0U;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		var height = Height.Mempool;
		var stx = new SmartTransaction(tx, height);
		var coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.False(coin.IsSufficientlyDistancedFromExternalKeys);

		// 2. Internal, no inputs ours. This is a strange case, it shouldn't happen normally. Maybe expecting false could help us with dust attacks?
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: true);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		stx = new SmartTransaction(tx, height);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.False(coin.IsSufficientlyDistancedFromExternalKeys);

		// 3. External, some inputs external ours:
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		var inHdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		var inCoin = BitcoinFactory.CreateSmartCoin(inHdpk, 1m);
		tx.Inputs.Add(inCoin.Outpoint);
		stx = new SmartTransaction(tx, height);
		stx.TryAddWalletInput(inCoin);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.False(coin.IsSufficientlyDistancedFromExternalKeys);

		// 4. Internal, some inputs external ours:
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: true);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		stx = new SmartTransaction(tx, height);
		stx.TryAddWalletInput(inCoin);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.False(coin.IsSufficientlyDistancedFromExternalKeys);

		// 5. External, some inputs internal ours. This is also a strange case. Usually it goes from external to internal, not the other way around.
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		inHdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: true);
		inCoin = BitcoinFactory.CreateSmartCoin(inHdpk, 1m);
		inCoin.Transaction.TryAddWalletInput(BitcoinFactory.CreateSmartCoin(km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false), 1m));
		tx.Inputs[1] = new TxIn(inCoin.Outpoint);
		stx = new SmartTransaction(tx, height);
		stx.TryAddWalletInput(inCoin);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.True(coin.IsSufficientlyDistancedFromExternalKeys);

		// 6. Internal, some inputs internal ours:
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: true);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		stx = new SmartTransaction(tx, height);
		stx.TryAddWalletInput(inCoin);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.True(coin.IsSufficientlyDistancedFromExternalKeys);

		// 7. External, some inputs internal ours, some inputs external ours:
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		var inHdpk2 = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: true);
		var inCoin2 = BitcoinFactory.CreateSmartCoin(inHdpk2, 1m);
		tx.Inputs.Add(inCoin2.Outpoint);
		stx = new SmartTransaction(tx, height);
		stx.TryAddWalletInput(inCoin);
		stx.TryAddWalletInput(inCoin2);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.True(coin.IsSufficientlyDistancedFromExternalKeys);

		// 8. Internal, some inputs internal ours, some inputs external ours:
		hdpk = km.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: true);
		tx.Outputs[0].ScriptPubKey = hdpk.P2wpkhScript;
		stx = new SmartTransaction(tx, height);
		stx.TryAddWalletInput(inCoin);
		stx.TryAddWalletInput(inCoin2);
		coin = new SmartCoin(stx, index, hdpk);
		BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(stx);
		Assert.True(coin.IsSufficientlyDistancedFromExternalKeys);
	}

	[Fact]
	public void SmartCoinImmatureTest()
	{
		var coinBaseTx = Transaction.Parse("010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff4f0327510c1362696e616e63652f3839345700000418572e8b066a61786e657420a584170a5545b9f9e18627d950d38f3cbefc9d6c7977259947ac326fe00fee1c066a61786e65745a9c0000c2000000ffffffff04f653a7260000000017a914ca35b1f4d02907314852f09935b9604507f8d700870000000000000000266a24aa21a9ed243c043949d4fdd7cfce809b7e4070c4a832da5d4cd0b137390b2e18d149696b00000000000000002b6a2952534b424c4f434b3a036b8396d36b05334061cf892fc5d21967011977486d73169b45b617004bfe5a00000000000000001976a914bc473af4c71c45d5aa3278adc99701ded3740a5488ac0120000000000000000000000000000000000000000000000000000000000000000000000000", Network.Main);
		var coinBaseStx = new SmartTransaction(coinBaseTx, new Height.ChainHeight(100u));
		var coinBaseCoin = new SmartCoin(coinBaseStx, 0, null!);

		// Negative bestHeight.
		// Assert.True(coinBaseCoin.Transaction.IsImmature(-100)); # impossible

		// Relatively negative bestHeight.
		Assert.True(coinBaseCoin.Transaction.IsImmature(0));

		// Same.
		Assert.True(coinBaseCoin.Transaction.IsImmature(100));

		// Almost mature.
		Assert.True(coinBaseCoin.Transaction.IsImmature(200));

		// Mature.
		Assert.False(coinBaseCoin.Transaction.IsImmature(201));

		// Mature.
		Assert.False(coinBaseCoin.Transaction.IsImmature(300));

		// Non-Coinbase tx.
		var tx = Transaction.Parse("01000000015cff8c26f4ed95b6db750c21fe1a150ee7c6fb629b7f97f77e641935e6109b31000000006a47304402204e4655953a5f3b13764563f673760d6f0d6a1837b01846f12f2ffdd819a1f21d022075bbc5be28aa32be69d17c3b5ca502264460387594c3b4bc66d65f130812613501210369e03e2c91f0badec46c9c903d9e9edae67c167b9ef9b550356ee791c9a40896ffffffff02dfaff10f000000001976a9149f21a07a0c7c3cf65a51f586051395762267cdaf88acb4062c000000000016001472196a5d64c66518fbe9555854ac7a6a4be902f200000000", Network.Main);
		var stx = new SmartTransaction(tx, new Height.ChainHeight(100));
		var coin = new SmartCoin(stx, 0, null!);

		// Whatever happens this should be always false.
		Assert.False(coin.Transaction.IsImmature(0));
		Assert.False(coin.Transaction.IsImmature(100));
		Assert.False(coin.Transaction.IsImmature(300));
	}
}
