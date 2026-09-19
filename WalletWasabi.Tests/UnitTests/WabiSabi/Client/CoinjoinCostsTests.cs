using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// The wallet's share of what a coinjoin cost, as shown on the coinjoin details screen.
/// </summary>
public class CoinjoinCostsTests
{
	private const decimal SatoshiPerByte = 10m;
	private static readonly Money InputFee = Money.Satoshis(SatoshiPerByte * Constants.P2wpkhInputVirtualSize);
	private static readonly Money OutputFee = Money.Satoshis(SatoshiPerByte * Constants.P2wpkhOutputVirtualSize);

	[Fact]
	public void WalletPaysForItsOwnInputsAndOutputs()
	{
		var builder = new CoinjoinBuilder();
		var myCoin = builder.AddInput(Money.Coins(1m));
		var theirCoin = builder.AddInput(Money.Coins(2m));

		// Everyone takes back exactly what they put in, minus what their own input and output cost.
		var myOutput = builder.AddOutput(myCoin.Amount - InputFee - OutputFee);
		builder.AddOutput(theirCoin.Amount - InputFee - OutputFee);

		var costs = CoinJoinClient.CalculateCosts(builder.Finalize(), [myCoin], [myOutput]);

		Assert.Equal(InputFee + OutputFee, costs.MiningFee);
		Assert.Equal(Money.Zero, costs.WastedDust);
		Assert.Equal(Money.Zero, costs.PaymentsTotal);
	}

	[Fact]
	public void WhatCannotBeDecomposedIntoAnOutputIsWastedDust()
	{
		var builder = new CoinjoinBuilder();
		var myCoin = builder.AddInput(Money.Coins(1m));
		var theirCoin = builder.AddInput(Money.Coins(2m));

		// 1000 satoshis of the wallet's money are left over, too small to be worth an output of their own.
		var leftOver = Money.Satoshis(1000);
		var myOutput = builder.AddOutput(myCoin.Amount - InputFee - OutputFee - leftOver);
		builder.AddOutput(theirCoin.Amount - InputFee - OutputFee);

		var costs = CoinJoinClient.CalculateCosts(builder.Finalize(), [myCoin], [myOutput]);

		Assert.Equal(InputFee + OutputFee, costs.MiningFee);
		Assert.Equal(leftOver, costs.WastedDust);
	}

	[Fact]
	public void OtherParticipantsCostsAreNotTheWalletsCosts()
	{
		var builder = new CoinjoinBuilder();
		var myCoin = builder.AddInput(Money.Coins(1m));
		var theirCoin = builder.AddInput(Money.Coins(2m));

		// The wallet registers two outputs, one of which is a payment to someone else's address.
		var myChange = builder.AddOutput(Money.Coins(0.5m));
		var myPayment = builder.AddOutput(myCoin.Amount - Money.Coins(0.5m) - InputFee - OutputFee - OutputFee);
		builder.AddOutput(theirCoin.Amount - InputFee - OutputFee);

		var costs = CoinJoinClient.CalculateCosts(builder.Finalize(), [myCoin], [myChange, myPayment]);

		// Two outputs of ours, so two output fees - and the payment is not wasted dust, it is money sent.
		Assert.Equal(InputFee + OutputFee + OutputFee, costs.MiningFee);
		Assert.Equal(Money.Zero, costs.WastedDust);
	}

	[Fact]
	public void AnOutputThatNeverMadeItIntoTheTransactionIsNotPaidFor()
	{
		var builder = new CoinjoinBuilder();
		var myCoin = builder.AddInput(Money.Coins(1m));
		var theirCoin = builder.AddInput(Money.Coins(2m));

		var myRegisteredOutput = builder.AddOutput(Money.Coins(0.5m));
		builder.AddOutput(theirCoin.Amount - InputFee - OutputFee);

		// The wallet asked for a second output but it never reached the transaction, so that money is gone.
		var myMissingOutput = new TxOut(myCoin.Amount - Money.Coins(0.5m) - InputFee - OutputFee - OutputFee, NewScript());

		var costs = CoinJoinClient.CalculateCosts(builder.Finalize(), [myCoin], [myRegisteredOutput, myMissingOutput]);

		Assert.Equal(InputFee + OutputFee, costs.MiningFee);
		Assert.Equal(myCoin.Amount - myRegisteredOutput.Value - costs.MiningFee, costs.WastedDust);
		Assert.True(costs.WastedDust > Money.Zero);
	}

	[Fact]
	public void TheWalletIsChargedExactlyWhatTheRoundChargesIt()
	{
		// WabiSabi charges each input and each output separately, so the wallet's share has to be
		// added up the same way - summing vsizes first would drift by a few satoshis.
		var builder = new CoinjoinBuilder();
		var myFirstCoin = builder.AddInput(Money.Coins(1m));
		var mySecondCoin = builder.AddInput(Money.Coins(0.3m));
		var theirCoin = builder.AddInput(Money.Coins(2m));

		var myOutput = builder.AddOutput(myFirstCoin.Amount + mySecondCoin.Amount - InputFee - InputFee - OutputFee);
		builder.AddOutput(theirCoin.Amount - InputFee - OutputFee);

		var signingState = builder.Finalize();
		var feeRate = signingState.Parameters.MiningFeeRate;

		var costs = CoinJoinClient.CalculateCosts(signingState, [myFirstCoin, mySecondCoin], [myOutput]);

		// What the round took off each input to issue its amount credentials, plus what the output cost to register.
		var chargedForInputs =
			(myFirstCoin.Amount - myFirstCoin.EffectiveValue(feeRate)) +
			(mySecondCoin.Amount - mySecondCoin.EffectiveValue(feeRate));
		var chargedForOutput = feeRate.GetFee(myOutput.ScriptPubKey.EstimateOutputVsize());

		Assert.Equal(chargedForInputs + chargedForOutput, costs.MiningFee);
		Assert.Equal(Money.Zero, costs.WastedDust);
	}

	[Fact]
	public void CostsOfSeveralCoinjoinsAddUp()
	{
		var first = new CoinjoinCosts(Money.Satoshis(100), Money.Satoshis(10), Money.Satoshis(1000));
		var second = new CoinjoinCosts(Money.Satoshis(200), Money.Satoshis(20), Money.Zero);

		var total = first + second;

		Assert.Equal(Money.Satoshis(300), total.MiningFee);
		Assert.Equal(Money.Satoshis(30), total.WastedDust);
		Assert.Equal(Money.Satoshis(1000), total.PaymentsTotal);

		// The payments are money sent, not a fee, so they stay out of the figure shown as "Fees".
		Assert.Equal(Money.Satoshis(330), total.TotalFee);
		Assert.Equal(total, CoinjoinCosts.Zero + total);
	}

	private static Script NewScript()
	{
		using var key = new Key();
		return key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
	}

	/// <summary>Builds the state a coinjoin transaction is created from, one registration at a time.</summary>
	private class CoinjoinBuilder
	{
		private readonly CoinJoinInputCommitmentData _commitment;
		private readonly KeyManager _keyManager;
		private readonly IKeyChain _keyChain;
		private ConstructionState _state;

		public CoinjoinBuilder()
		{
			var parameters = WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig()) with
			{
				MiningFeeRate = new FeeRate(SatoshiPerByte)
			};
			var round = WabiSabiFactory.CreateRound(parameters);

			_keyManager = ServiceFactory.CreateKeyManager("");
			_keyChain = new KeyChain(_keyManager, "");
			_commitment = new CoinJoinInputCommitmentData(parameters.CoordinationIdentifier, round.Id);
			_state = new ConstructionState(parameters);
		}

		public Coin AddInput(Money amount)
		{
			var smartCoin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(_keyManager), amount);
			_state = _state.AddInput(smartCoin.Coin, _keyChain.GetOwnershipProof(smartCoin, _commitment), _commitment);
			return smartCoin.Coin;
		}

		public TxOut AddOutput(Money amount)
		{
			var output = new TxOut(amount, NewScript());
			_state = _state.AddOutput(output);
			return output;
		}

		public SigningState Finalize() => _state.Finalize();
	}
}
