using NBitcoin;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

public class ConstructionStateTests
{
	[Fact]
	public void ConstructionStateFeeRateCalculation()
	{
		var miningFeeRate = new FeeRate(8m);
		var cfg = new WabiSabiConfig();
		var roundParameters = RoundParameters.Create(cfg, miningFeeRate);

		var round = WabiSabiFactory.CreateRound(roundParameters);
		var state = round.Assert<ConstructionState>();

		var (coin, ownershipProof) = WabiSabiFactory.CreateCoinWithOwnershipProof(
			amount: roundParameters.AllowedInputAmounts.Min + miningFeeRate.GetFee(Constants.P2wpkhInputVirtualSize + Constants.P2wpkhOutputVirtualSize),
			roundId: round.Id);
		state = state.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));
		state = state.AddOutput(new TxOut(roundParameters.AllowedInputAmounts.Min, new Script("0 bf3593d140d512eb607b3ddb5c5ee085f1e3a210")));

		var signingState = state.Finalize();
		Assert.Equal(miningFeeRate, signingState.EffectiveFeeRate);
	}

	/// <summary>
	/// Round of https://github.com/WalletWasabi/WalletWasabi/issues/15090. A single client spent all its credentials,
	/// so the coinjoin pays exactly the rounded down fee of every input and output: 848 sats instead of 849.58 sats.
	/// </summary>
	[Theory]
	[InlineData(26_614, true)]
	[InlineData(26_615, false)]
	public void FinalizeAcceptsFeesChargedByCredentials(long changeAmount, bool isAccepted)
	{
		var miningFeeRate = new FeeRate(1.07m);
		var roundParameters = RoundParameters.Create(new WabiSabiConfig(), miningFeeRate);
		var round = WabiSabiFactory.CreateRound(roundParameters);
		var state = round.Assert<ConstructionState>();
		using Key key = new();

		long[] inputAmounts = [462_007, 257_327, 169_506, 147_348, 131_004, 59_892, 19_932, 19_886, 19_609];
		foreach (var amount in inputAmounts)
		{
			var coin = WabiSabiFactory.CreateCoin(key, Money.Satoshis(amount), ScriptPubKeyType.TaprootBIP86);
			var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id, ScriptPubKeyType.TaprootBIP86);
			state = state.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));
		}

		(long Amount, ScriptPubKeyType Type)[] outputs =
		[
			.. Enumerable.Repeat((200_000L, ScriptPubKeyType.Segwit), 6),
			(59_049, ScriptPubKeyType.TaprootBIP86),
			(changeAmount, ScriptPubKeyType.TaprootBIP86)
		];
		foreach (var (amount, type) in outputs)
		{
			state = state.AddOutput(new TxOut(Money.Satoshis(amount), key.PubKey.GetScriptPubKey(type)));
		}

		if (isAccepted)
		{
			Assert.Equal(Money.Satoshis(848), state.Balance);
			state.Finalize();
		}
		else
		{
			var ex = Assert.Throws<WabiSabiProtocolException>(() => state.Finalize());
			Assert.Equal(WabiSabiProtocolErrorCode.InsufficientFees, ex.ErrorCode);
		}
	}
}
