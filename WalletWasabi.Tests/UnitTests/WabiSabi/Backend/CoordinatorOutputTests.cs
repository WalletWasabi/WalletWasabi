using NBitcoin;
using System.Linq;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
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
	public void TrimIntoParticipantDenominations()
	{
		Money[] participantOutputValues = [Money.Satoshis(32_768), Money.Satoshis(10_000), Money.Satoshis(6_561)];
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(43_390), participantOutputValues);

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		// Trimming into the next lowest denomination participants have (32_768) would give up 10_622 satoshis to the miners, these give up 268.
		Assert.Equal([10_000, 10_000, 10_000, 6_561, 6_561], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));

		// Trimming only ever increases the mining fee, so the round's fee rate must still be satisfied.
		withCoordinatorOutputs.Finalize();
	}

	[Fact]
	public void DontTrimIntoDenominationsParticipantsDontHave()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(43_390), []);

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([43_390], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void TrimIntoNextLowestDenominationWithOneScript()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(43_390), EveryDenominationUpTo(43_390));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts[..1], trimCoordinatorOutput: true);

		Assert.Equal([39_366], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void TrimIntoDenominationsKeepingTheMost()
	{
		Money[] participantOutputValues = [Money.Satoshis(32_768), Money.Satoshis(19_683), Money.Satoshis(13_122)];
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(33_000), participantOutputValues);

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		// 32_768 alone would give up 232 satoshis to the miners, these give up 195, even though they cost more to spend later on.
		Assert.Equal([19_683, 13_122], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Theory]
	[InlineData(1.0, 43_390)]
	[InlineData(1.9, 43_390)]
	[InlineData(2.7, 123_456)]
	[InlineData(13.3, 987_654)]
	[InlineData(1.9, 30_058)] // 20_000 + 10_000 + the fee of the second output, which GetFee rounds down from 58.9 to 58.
	public void TrimIntoDenominationsKeepsMoreAndPaysTheRoundFeeRate(double miningFeeRate, long coordinatorOutputValue)
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(coordinatorOutputValue), EveryDenominationUpTo(coordinatorOutputValue), miningFeeRate: new FeeRate((decimal)miningFeeRate));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);
		var withCoordinatorOutput = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts[..1], trimCoordinatorOutput: true);

		// With one script the coordinator output is trimmed into the next lowest denomination, as before decomposing.
		Assert.True(GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts).Sum() > GetCoordinatorOutputValues(withCoordinatorOutput, coordinatorScripts).Sum());
		withCoordinatorOutputs.Finalize();
	}

	[Fact]
	public void TrimIntoNoMoreOutputsThanTheTransactionFits()
	{
		// Fill the coinjoin with registered outputs until only two more fit.
		var participantOutputValues = EveryDenominationUpTo(43_390);
		var (emptyRound, emptyCoinjoin, emptyCoordinatorScripts) = CreateCoinjoin(Money.Satoshis(43_390), participantOutputValues);
		var outputVsize = emptyCoordinatorScripts[0].EstimateOutputVsize();
		var fillerOutputCount = ((emptyRound.Parameters.MaxTransactionSize - emptyCoinjoin.EstimatedVsize) / outputVsize) - 2;
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(43_390), [.. participantOutputValues, .. Enumerable.Repeat(DefaultMinRegistrableAmount, fillerOutputCount)]);

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		// Without the limit the coordinator would create four outputs: 20_000, 10_000, 8_192 and 5_000.
		Assert.Equal([32_768, 10_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
		withCoordinatorOutputs.Finalize();
	}

	[Fact]
	public void DontTakeDustCoordinationFee()
	{
		// 200 satoshis are worth spending at 1 sat/vbyte, but it is still a dust output.
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(200), [], minRegistrableAmount: Money.Satoshis(100));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: false);

		Assert.Empty(GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
		withCoordinatorOutputs.Finalize();
	}

	[Fact]
	public void DontTrimWhenDisabled()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(12_345), EveryDenominationUpTo(12_345));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: false);

		Assert.Equal([12_345], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimDenomination()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(10_000), EveryDenominationUpTo(10_000));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([10_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimSmallestDenomination()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(5_000), EveryDenominationUpTo(5_000));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([5_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimBelowRoundMinimum()
	{
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(4_999), EveryDenominationUpTo(4_999));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		// Participants can't register outputs this small either, so there is no denomination to hide among.
		Assert.Equal([4_999], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimWhenNoDenominationFits()
	{
		// There is no standard denomination between 5_001 and 6_000 satoshis.
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(6_000), [Money.Satoshis(6_561)], minRegistrableAmount: Money.Satoshis(5_001));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([6_000], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	[Fact]
	public void DontTrimIntoUneconomicalOutput()
	{
		// At this fee rate an output has to hold more than 10_516 satoshis to be worth spending later on, so trimming
		// down to the 10_000 denomination would leave the coordinator with an output that costs more than it is worth.
		var (round, coinjoin, coordinatorScripts) = CreateCoinjoin(Money.Satoshis(12_345), EveryDenominationUpTo(12_345), miningFeeRate: new FeeRate(337m));

		var withCoordinatorOutputs = Arena.AddCoordinationFee(round, coinjoin, coordinatorScripts, trimCoordinatorOutput: true);

		Assert.Equal([12_345], GetCoordinatorOutputValues(withCoordinatorOutputs, coordinatorScripts));
	}

	private static Money[] EveryDenominationUpTo(long value) =>
		DenominationBuilder.CreateDenominationAmounts(DefaultMinRegistrableAmount, Money.Satoshis(value)).ToArray();

	private static long[] GetCoordinatorOutputValues(ConstructionState coinjoin, Script[] coordinatorScripts)
	{
		var coordinatorOutputs = coinjoin.Outputs.Where(x => coordinatorScripts.Contains(x.ScriptPubKey)).ToArray();

		// Every coordinator output must have its own script.
		Assert.Equal(coordinatorOutputs.Length, coordinatorOutputs.Select(x => x.ScriptPubKey).Distinct().Count());

		return coordinatorOutputs.Select(x => x.Value.Satoshi).OrderDescending().ToArray();
	}

	/// <summary>
	/// Creates a coinjoin with a single input, a registered output and one more registered output for each of
	/// <paramref name="participantOutputValues"/>, leaving exactly <paramref name="coordinatorOutputValue"/> satoshis for a single coordinator output.
	/// </summary>
	private static (Round Round, ConstructionState Coinjoin, Script[] CoordinatorScripts) CreateCoinjoin(
		Money coordinatorOutputValue,
		Money[] participantOutputValues,
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
		var coordinatorScripts = Enumerable.Range(0, Arena.MaxCoordinatorOutputs).Select(_ => BitcoinFactory.CreateScript()).ToArray();
		var bobScript = BitcoinFactory.CreateScript();
		var participantScript = BitcoinFactory.CreateScript();

		using Key key = new();
		var (coin, ownershipProof) = WabiSabiFactory.CreateCoinWithOwnershipProof(key, inputAmount);

		ConstructionState Build(Money bobValue) =>
			participantOutputValues.Aggregate(
				new ConstructionState(parameters)
					.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData())
					.AddOutput(new TxOut(bobValue, bobScript)),
				(coinjoin, value) => coinjoin.AddOutput(new TxOut(value, participantScript)));

		// The size of the coinjoin doesn't depend on the values of its outputs, so the mining fee the coordinator
		// has to leave behind can be calculated with an arbitrary value already registered.
		var sizeToPayFor = Build(inputAmount).EstimatedVsize + coordinatorScripts[0].EstimateOutputVsize();
		var miningFee = parameters.MiningFeeRate.GetFee(sizeToPayFor) + Money.Satoshis(1);

		return (WabiSabiFactory.CreateRound(parameters), Build(inputAmount - miningFee - participantOutputValues.Sum() - coordinatorOutputValue), coordinatorScripts);
	}
}
