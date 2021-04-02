using NBitcoin;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models
{
	public class MultipartyTransactionTests
	{
		static MoneyRange DefaultAllowedAmounts = new MoneyRange(Money.Zero, Money.Coins(1));
		static Parameters DefaultParameters = new Parameters(FeeRate.Zero, DefaultAllowedAmounts, DefaultAllowedAmounts, Network.Main);

		[Fact]
		public void TwoPartyNoFees()
		{
			using Key key1 = new();
			using Key key2 = new();

			var alice1 = WabiSabiFactory.CreateAlice(key: key1);
			var alice2 = WabiSabiFactory.CreateAlice(key: key2);

			var state = new Construction(DefaultParameters);

			Assert.Empty(state.Inputs);
			Assert.Empty(state.Outputs);

			var oneInput = state.AddInput(alice1.Coins.First());

			Assert.Single(oneInput.Inputs);
			Assert.Empty(oneInput.Outputs);

			// Previous state should be unmodified
			Assert.Empty(state.Inputs);
			Assert.Empty(state.Outputs);

			var differentInput = state.AddInput(alice2.Coins.First());

			Assert.Single(differentInput.Inputs);
			Assert.Empty(differentInput.Outputs);
			Assert.NotEqual(oneInput.Inputs, differentInput.Inputs);
			Assert.Equal(oneInput.Outputs, differentInput.Outputs);

			var twoInputs = oneInput.AddInput(alice2.Coins.First());

			Assert.Equal(2, twoInputs.Inputs.Count());
			Assert.Empty(twoInputs.Outputs);

			// address reuse bad
			var bob1 = new TxOut(Money.Coins(1), alice1.Coins.First().ScriptPubKey);
			var withOutput = twoInputs.AddOutput(bob1);

			Assert.Equal(2, withOutput.Inputs.Count());
			Assert.Single(withOutput.Outputs);

			var bob2 = new TxOut(Money.Coins(1), alice2.Coins.First().ScriptPubKey);
			var noFeeTx = withOutput.AddOutput(bob2).Finalize();
			Assert.Equal(Money.Zero, noFeeTx.Balance);

			var tx = noFeeTx.CreateUnsignedTransaction();
			Assert.Equal(2, tx.Inputs.Count());
			Assert.Equal(2, tx.Outputs.Count);
			Assert.Contains(alice1.Coins.First().Outpoint, tx.Inputs.Select(x => x.PrevOut));
			Assert.Contains(alice2.Coins.First().Outpoint, tx.Inputs.Select(x => x.PrevOut));
			Assert.Contains(alice1.Coins.First().ScriptPubKey, tx.Outputs.Select(x => x.ScriptPubKey));
			Assert.Contains(alice2.Coins.First().ScriptPubKey, tx.Outputs.Select(x => x.ScriptPubKey));

			var alice1Tx = tx.Clone();
			alice1Tx.Sign(key1.GetBitcoinSecret(Network.Main), alice1.Coins.First());

			var alice1Sig = noFeeTx.AddWitness(0, alice1Tx.Inputs[0].WitScript);
			Assert.False(alice1Sig.IsFullySigned);
			Assert.Equal(alice1Tx.ToString(), alice1Sig.CreateTransaction().ToString());

			var alice2Tx = tx.Clone();
			alice2Tx.Sign(key2.GetBitcoinSecret(Network.Main), alice2.Coins.First());

			var alice2Sig = alice1Sig.AddWitness(1, alice2Tx.Inputs[1].WitScript);
			Assert.True(alice2Sig.IsFullySigned);

			var signed = alice2Sig.CreateTransaction();
			Assert.NotEqual(alice1Tx.ToString(), signed.ToString());
			Assert.NotEqual(alice2Tx.ToString(), signed.ToString());
			Assert.True(signed.Inputs.All(x => x.HasWitScript()));
		}

		[Fact]
		public void AddWithOptimize()
		{
			var alice = WabiSabiFactory.CreateAlice();

			var state = new Construction(DefaultParameters).AddInput(alice.Coins.First());

			var script = BitcoinFactory.CreateScript();
			var bob = new TxOut(alice.Coins.First().Amount/2, script);
			var withOutput = state.AddOutput(bob);
			var duplicateOutputNoFee = withOutput.AddOutput(bob).Finalize();

			var tx2 = duplicateOutputNoFee.CreateUnsignedTransaction();
			Assert.Equal(1, tx2.Outputs.Count);
			Assert.Contains(script, tx2.Outputs.Select(x => x.ScriptPubKey));
			Assert.Equal(alice.Coins.First().Amount, tx2.Outputs[0].Value);
		}

		[Fact]
		public void WitnessValidation()
		{
			using Key key1 = new();
			using Key key2 = new();

			var alice1 = WabiSabiFactory.CreateAlice(key: key1);
			var alice2 = WabiSabiFactory.CreateAlice(key: key2);

			var state = new Construction(DefaultParameters).AddInput(alice1.Coins.First()).AddInput(alice2.Coins.First());

			// address reuse bad
			var bob1 = new TxOut(Money.Coins(1), alice1.Coins.First().ScriptPubKey);
			var withOutput = state.AddOutput(bob1);

			var bob2 = new TxOut(Money.Coins(1), alice2.Coins.First().ScriptPubKey);
			var noFeeTx = withOutput.AddOutput(bob2).Finalize();

			var tx = noFeeTx.CreateUnsignedTransaction();

			var alice1Tx = tx.Clone();
			alice1Tx.Sign(key1.GetBitcoinSecret(Network.Main), alice1.Coins.First());
			var alice2Tx = tx.Clone();
			alice2Tx.Sign(key2.GetBitcoinSecret(Network.Main), alice2.Coins.First());

			// Only accept valid witnesses
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, Assert.Throws<WabiSabiProtocolException>(() => noFeeTx.AddWitness(1, alice1Tx.Inputs[0].WitScript)).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, Assert.Throws<WabiSabiProtocolException>(() => noFeeTx.AddWitness(1, alice1Tx.Inputs[1].WitScript)).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, Assert.Throws<WabiSabiProtocolException>(() => noFeeTx.AddWitness(0, alice1Tx.Inputs[1].WitScript)).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, Assert.Throws<WabiSabiProtocolException>(() => noFeeTx.AddWitness(0, alice2Tx.Inputs[0].WitScript)).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, Assert.Throws<WabiSabiProtocolException>(() => noFeeTx.AddWitness(0, alice2Tx.Inputs[1].WitScript)).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, Assert.Throws<WabiSabiProtocolException>(() => noFeeTx.AddWitness(1, alice2Tx.Inputs[0].WitScript)).ErrorCode);

			// Add Alice 1's signature
			var alice1Sig = noFeeTx.AddWitness(0, alice1Tx.Inputs[0].WitScript);
			Assert.False(alice1Sig.IsFullySigned);

			// Witness can only be accepted once per input
			Assert.Equal(WabiSabiProtocolErrorCode.WitnessAlreadyProvided, Assert.Throws<WabiSabiProtocolException>(() => alice1Sig.AddWitness(0, alice1Tx.Inputs[0].WitScript)).ErrorCode);
		}

		[Fact]
		public void FeeRateValidation()
		{
			var feeRate = new FeeRate(new Money(1000L));

			using Key key1 = new();
			using Key key2 = new();

			var alice1 = WabiSabiFactory.CreateAlice(key: key1);
			var alice2 = WabiSabiFactory.CreateAlice(key: key2);

			var state = new Construction(DefaultParameters with { FeeRate = feeRate })
				.AddInput(alice1.Coins.First())
				.AddInput(alice2.Coins.First());

			var bob1 = new TxOut(Money.Coins(1), alice1.Coins.First().ScriptPubKey);
			var withOutput = state.AddOutput(bob1);

			Assert.Equal(2, withOutput.Inputs.Count());
			Assert.Single(withOutput.Outputs);

			var bob2 = new TxOut(Money.Coins(1), alice2.Coins.First().ScriptPubKey);
			Assert.Equal(WabiSabiProtocolErrorCode.InsufficientFees, Assert.Throws<WabiSabiProtocolException>(() => withOutput.AddOutput(bob2).Finalize()).ErrorCode);

			bob2 = new TxOut(Money.Coins(0.9999m), alice2.Coins.First().ScriptPubKey);
			var generousFeeTx = withOutput.AddOutput(bob2).Finalize();

			var tx = generousFeeTx.CreateUnsignedTransaction();
			Assert.Equal(2, tx.Inputs.Count);
			Assert.Equal(2, tx.Outputs.Count);
			Assert.Contains(alice1.Coins.First().Outpoint, tx.Inputs.Select(x => x.PrevOut));
			Assert.Contains(alice2.Coins.First().Outpoint, tx.Inputs.Select(x => x.PrevOut));
			Assert.Contains(alice1.Coins.First().ScriptPubKey, tx.Outputs.Select(x => x.ScriptPubKey));
			Assert.Contains(alice2.Coins.First().ScriptPubKey, tx.Outputs.Select(x => x.ScriptPubKey));

			var alice1Tx = tx.Clone();
			alice1Tx.Sign(key1.GetBitcoinSecret(Network.Main), alice1.Coins.First());

			var alice1Sig = generousFeeTx.AddWitness(0, alice1Tx.Inputs[0].WitScript);
			Assert.False(alice1Sig.IsFullySigned);
			Assert.Equal(alice1Tx.ToString(), alice1Sig.CreateTransaction().ToString());

			var alice2Tx = tx.Clone();
			alice2Tx.Sign(key2.GetBitcoinSecret(Network.Main), alice2.Coins.First());

			var alice2Sig = alice1Sig.AddWitness(1, alice2Tx.Inputs[1].WitScript);
			Assert.True(alice2Sig.IsFullySigned);

			var signed = alice2Sig.CreateTransaction();
			Assert.NotEqual(alice1Tx.ToString(), signed.ToString());
			Assert.NotEqual(alice2Tx.ToString(), signed.ToString());
			Assert.True(signed.Inputs.All(x => x.HasWitScript()));

			var coins = new[] { alice1.Coins.First(), alice2.Coins.First() };
			Assert.True(signed.GetVirtualSize() < generousFeeTx.EstimatedVsize);
			Assert.True(feeRate <= signed.GetFeeRate(coins));
			Assert.Equal(generousFeeTx.Balance, signed.GetFee(coins));
		}

		[Fact]
		public void NoDuplicateInputs()
		{
			var alice = WabiSabiFactory.CreateAlice();
			var state = new Construction(DefaultParameters).AddInput(alice.Coins.First());
			Assert.Equal(WabiSabiProtocolErrorCode.NonUniqueInputs, Assert.Throws<WabiSabiProtocolException>(() => state.AddInput(alice.Coins.First())).ErrorCode);
			Assert.Single(state.Inputs);
		}

		// TODO nonstandard input

		[Fact]
		public void OnlyAllowedInputTypes()
		{
			var legacyOnly = new Construction(DefaultParameters with { AllowedInputTypes = ImmutableSortedSet<ScriptType>.Empty.Add(ScriptType.P2PKH) });
			var alice = WabiSabiFactory.CreateAlice();
			Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, Assert.Throws<WabiSabiProtocolException>(() => legacyOnly.AddInput(alice.Coins.First())).ErrorCode);
		}

		[Fact]
		public void InputAmountRanges()
		{
			var alice = WabiSabiFactory.CreateAlice();
			var coin = alice.Coins.First();

			var exact = new Construction(DefaultParameters with { AllowedInputAmounts = new MoneyRange(coin.Amount, coin.Amount) });
			var above = new Construction(DefaultParameters with { AllowedInputAmounts = new MoneyRange(2 * coin.Amount, 3 * coin.Amount) });
			var below = new Construction(DefaultParameters with { AllowedInputAmounts = new MoneyRange(coin.Amount - Money.Coins(0.001m), coin.Amount - Money.Coins(0.0001m)) });

			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, Assert.Throws<WabiSabiProtocolException>(() => above.AddInput(alice.Coins.First())).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, Assert.Throws<WabiSabiProtocolException>(() => below.AddInput(alice.Coins.First())).ErrorCode);

			// Allowed range is inclusive:
			exact.AddInput(alice.Coins.First());
		}

		[Fact]
		public void UneconomicalInputs()
		{
			var alice1 = WabiSabiFactory.CreateAlice(value: new Money(1000L));
			var alice1Coin = alice1.Coins.First();

			var alice2 = WabiSabiFactory.CreateAlice(value: new Money(1001L));
			var alice2Coin = alice2.Coins.First();

			// requires 1k sats per input in sat/vKB
			var inputVsize = alice1Coin.ScriptPubKey.EstimateInputVsize();
			var feeRate = new FeeRate(new Money((1_000_000L + inputVsize-1) / inputVsize));
			Assert.Equal(new Money(1000L), feeRate.GetFee(alice1Coin.ScriptPubKey.EstimateInputVsize()));

			var state = new Construction(DefaultParameters with { FeeRate = feeRate });

			ThrowsProtocolException(WabiSabiProtocolErrorCode.UneconomicalInput, () => state.AddInput(alice1Coin));

			Assert.Equal(alice2Coin.Amount, Assert.Single(state.AddInput(alice2Coin).Inputs).Amount);
		}

		[Fact]
		public void NoNonStandardOutput()
		{
			var state = new Construction(DefaultParameters);
			var sha256Bounty = new TxOut(Money.Coins(1), Script.FromHex("aa20000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f87"));
			Assert.Equal(WabiSabiProtocolErrorCode.NonStandardOutput, Assert.Throws<WabiSabiProtocolException>(() => state.AddOutput(sha256Bounty)).ErrorCode);
		}

		[Fact]
		public void OnlyAllowedOutputTypes()
		{
			var legacyOnly = new Construction(DefaultParameters with { AllowedOutputTypes = ImmutableSortedSet<ScriptType>.Empty.Add(ScriptType.P2PKH) });
			var p2wpkh = BitcoinFactory.CreateScript();
			Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, Assert.Throws<WabiSabiProtocolException>(() => legacyOnly.AddOutput(new TxOut(Money.Coins(1), p2wpkh))).ErrorCode);
		}

		[Fact]
		public void OutputAmountRanges()
		{
			var costly = new Construction(DefaultParameters with { AllowedOutputAmounts = new MoneyRange(Money.Coins(7), Money.Coins(11)) });

			var p2wpkh = BitcoinFactory.CreateScript();
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, Assert.Throws<WabiSabiProtocolException>(() => costly.AddOutput(new TxOut(Money.Coins(1), p2wpkh))).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, Assert.Throws<WabiSabiProtocolException>(() => costly.AddOutput(new TxOut(Money.Coins(12), p2wpkh))).ErrorCode);

			// Allowed range is inclusive:
			costly.AddOutput(new TxOut(Money.Coins(7), p2wpkh));
			costly.AddOutput(new TxOut(Money.Coins(11), p2wpkh));
		}

		[Fact]
		public void NoDustOutputs()
		{
			var state = new Construction(DefaultParameters);

			var p2wpkh = BitcoinFactory.CreateScript();
			Assert.Equal(WabiSabiProtocolErrorCode.DustOutput, Assert.Throws<WabiSabiProtocolException>(() => state.AddOutput(new TxOut(new Money(1L), p2wpkh))).ErrorCode);
			Assert.Equal(WabiSabiProtocolErrorCode.DustOutput, Assert.Throws<WabiSabiProtocolException>(() => state.AddOutput(new TxOut(new Money(293L), p2wpkh))).ErrorCode);

			var output = new TxOut(new Money(294L), p2wpkh);
			var updated = state.AddOutput(output);
			Assert.Equal(output, Assert.Single(updated.Outputs));
		}
	}
}
