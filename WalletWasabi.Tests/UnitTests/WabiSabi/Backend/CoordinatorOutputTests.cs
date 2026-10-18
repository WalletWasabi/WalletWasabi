using NBitcoin;
using System.Linq;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

/// <summary>
/// Tests for the outputs the coordinator adds to hold the amount the participants could not decompose.
/// </summary>
public class CoordinatorOutputTests
{
	private static readonly Money DefaultMinRegistrableAmount = Money.Satoshis(5_000);
	private static readonly FeeRate DefaultMiningFeeRate = new(1m);

	[Fact]
	public void TrimIntoDenominations()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(43_390));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		// Trimming into the next lowest denomination (39_366) would give up 4_024 satoshis to the miners, these give up 198.
		Assert.Equal([20_000, 10_000, 8_192, 5_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));

		// Trimming only ever increases the mining fee, so the round's fee rate must still be satisfied.
		withCoordinatorOutputs.Finalize();
	}

	[Fact]
	public void TrimIntoNextLowestDenominationWithOneScript()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(43_390));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts[..1], trimCoordinatorOutput: true);

		Assert.Equal([39_366], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Theory]
	[InlineData(1.0, 43_390)]
	[InlineData(1.9, 43_390)]
	[InlineData(2.7, 123_456)]
	[InlineData(13.3, 987_654)]
	[InlineData(1.9, 30_058)] // 20_000 + 10_000 + the fee of the second output, which GetFee rounds down from 58.9 to 58.
	public void TrimIntoDenominationsPaysTheRoundFeeRate(double miningFeeRate, long coordinatorOutputValue)
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(coordinatorOutputValue), miningFeeRate: new FeeRate((decimal)miningFeeRate));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.True(GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts).Length > 1);
		withCoordinatorOutputs.Finalize();
	}

	[Fact]
	public void DontTrimWhenDisabled()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(12_345));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: false);

		Assert.Equal([12_345], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimDenomination()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(10_000));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([10_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimSmallestDenomination()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(5_000));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([5_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimBelowRoundMinimum()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(4_999));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		// Participants can't register outputs this small either, so there is no denomination to hide among.
		Assert.Equal([4_999], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimWhenNoDenominationFits()
	{
		// There is no standard denomination between 5_001 and 6_000 satoshis.
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(6_000), minRegistrableAmount: Money.Satoshis(5_001));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([6_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimIntoUneconomicalOutput()
	{
		// At this fee rate an output has to hold more than 10_516 satoshis to be worth spending later on, so trimming
		// down to the 10_000 denomination would leave the coordinator with an output that costs more than it is worth.
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(coordinatorOutputValue: Money.Satoshis(12_345), miningFeeRate: new FeeRate(337m));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([12_345], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	private static long[] GetCoordinatorOutputValues(ConstructionState coinjoin, Script[] coordinatorScripts)
	{
		var coordinatorOutputs = coinjoin.Outputs.Where(x => coordinatorScripts.Contains(x.ScriptPubKey)).ToArray();

		// Every coordinator output must have its own script.
		Assert.Equal(coordinatorOutputs.Length, coordinatorOutputs.Select(x => x.ScriptPubKey).Distinct().Count());

		return coordinatorOutputs.Select(x => x.Value.Satoshi).OrderDescending().ToArray();
	}

	/// <summary>
	/// Creates a coinjoin with a single input and a single registered output, leaving exactly
	/// <paramref name="coordinatorOutputValue"/> satoshis for a single coordinator output.
	/// </summary>
	private static (Round Round, ConstructionState Coinjoin, Script[] CoordinatorScripts) CreateCoinjoin(
		Money coordinatorOutputValue,
		Money? minRegistrableAmount = null,
		FeeRate? miningFeeRate = null)
	{
		var parameters = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = minRegistrableAmount ?? DefaultMinRegistrableAmount,
			MaxRegistrableAmount = Money.Coins(43_000m),
			MaxSuggestedAmountBase = Money.Coins(Constants.MaximumNumberOfBitcoins)
		}) with
		{
			MiningFeeRate = miningFeeRate ?? DefaultMiningFeeRate
		};

		var inputAmount = Money.Coins(1m);
		var coordinatorScripts = Enumerable.Range(0, 8).Select(_ => BitcoinFactory.CreateScript()).ToArray();
		var bobScript = BitcoinFactory.CreateScript();

		using Key key = new();
		var (coin, ownershipProof) = WabiSabiFactory.CreateCoinWithOwnershipProof(key, inputAmount);

		ConstructionState Build(Money bobValue) =>
			new ConstructionState(parameters)
				.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData())
				.AddOutput(new TxOut(bobValue, bobScript));

		// The size of the coinjoin doesn't depend on the values of its outputs, so the mining fee the coordinator
		// has to leave behind can be calculated with an arbitrary value already registered.
		var sizeToPayFor = Build(parameters.AllowedOutputAmounts.Min).EstimatedVsize + coordinatorScripts[0].EstimateOutputVsize();
		var miningFee = parameters.MiningFeeRate.GetFee(sizeToPayFor) + Money.Satoshis(1);

		return (WabiSabiFactory.CreateRound(parameters), Build(inputAmount - miningFee - coordinatorOutputValue), coordinatorScripts);
	}
}
