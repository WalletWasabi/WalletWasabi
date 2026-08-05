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
/// Tests for the output the coordinator adds to hold the amount the participants could not decompose.
/// </summary>
public class AnchorOutputTests
{
	private static readonly Money DefaultMinRegistrableAmount = Money.Satoshis(5_000);
	private static readonly FeeRate DefaultMiningFeeRate = new(1m);

	[Fact]
	public void ShaveIntoNextLowestDenomination()
	{
		var (round, coinjoin, coordinatorScript) = CreateCoinjoin(anchorValue: Money.Satoshis(12_345));

		var withAnchor = Arena.AddCoordinationFee(round, coinjoin, coordinatorScript, shaveAnchorOutputIntoDenomination: true);

		// 10_000 is the next lowest standard denomination, the remaining 2_345 satoshis are given up to the miners.
		Assert.Equal(Money.Satoshis(10_000), GetAnchorOutput(withAnchor, coordinatorScript).Value);

		// Shaving only ever increases the mining fee, so the round's fee rate must still be satisfied.
		withAnchor.Finalize();
	}

	[Fact]
	public void DontShaveWhenDisabled()
	{
		var (round, coinjoin, coordinatorScript) = CreateCoinjoin(anchorValue: Money.Satoshis(12_345));

		var withAnchor = Arena.AddCoordinationFee(round, coinjoin, coordinatorScript);

		Assert.Equal(Money.Satoshis(12_345), GetAnchorOutput(withAnchor, coordinatorScript).Value);
	}

	[Fact]
	public void DontShaveDenomination()
	{
		var (round, coinjoin, coordinatorScript) = CreateCoinjoin(anchorValue: Money.Satoshis(10_000));

		var withAnchor = Arena.AddCoordinationFee(round, coinjoin, coordinatorScript, shaveAnchorOutputIntoDenomination: true);

		Assert.Equal(Money.Satoshis(10_000), GetAnchorOutput(withAnchor, coordinatorScript).Value);
	}

	[Fact]
	public void DontShaveBelowRoundMinimum()
	{
		var (round, coinjoin, coordinatorScript) = CreateCoinjoin(anchorValue: Money.Satoshis(4_999));

		var withAnchor = Arena.AddCoordinationFee(round, coinjoin, coordinatorScript, shaveAnchorOutputIntoDenomination: true);

		// Participants can't register outputs this small either, so there is no denomination to hide among.
		Assert.Equal(Money.Satoshis(4_999), GetAnchorOutput(withAnchor, coordinatorScript).Value);
	}

	[Fact]
	public void DontShaveWhenNoDenominationFits()
	{
		// There is no standard denomination between 5_001 and 6_000 satoshis.
		var (round, coinjoin, coordinatorScript) = CreateCoinjoin(anchorValue: Money.Satoshis(6_000), minRegistrableAmount: Money.Satoshis(5_001));

		var withAnchor = Arena.AddCoordinationFee(round, coinjoin, coordinatorScript, shaveAnchorOutputIntoDenomination: true);

		Assert.Equal(Money.Satoshis(6_000), GetAnchorOutput(withAnchor, coordinatorScript).Value);
	}

	[Fact]
	public void DontShaveIntoUneconomicalOutput()
	{
		// At this fee rate an output has to hold more than 10_516 satoshis to be worth spending later on, so shaving
		// down to the 10_000 denomination would leave the coordinator with an output that costs more than it is worth.
		var (round, coinjoin, coordinatorScript) = CreateCoinjoin(anchorValue: Money.Satoshis(12_345), miningFeeRate: new FeeRate(337m));

		var withAnchor = Arena.AddCoordinationFee(round, coinjoin, coordinatorScript, shaveAnchorOutputIntoDenomination: true);

		Assert.Equal(Money.Satoshis(12_345), GetAnchorOutput(withAnchor, coordinatorScript).Value);
	}

	private static TxOut GetAnchorOutput(ConstructionState coinjoin, Script coordinatorScript) =>
		Assert.Single(coinjoin.Outputs, x => x.ScriptPubKey == coordinatorScript);

	/// <summary>
	/// Creates a coinjoin with a single input and a single registered output, leaving exactly
	/// <paramref name="anchorValue"/> satoshis for the coordinator's anchor output.
	/// </summary>
	private static (Round Round, ConstructionState Coinjoin, Script CoordinatorScript) CreateCoinjoin(
		Money anchorValue,
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
		var coordinatorScript = BitcoinFactory.CreateScript();
		var bobScript = BitcoinFactory.CreateScript();

		using Key key = new();
		var (coin, ownershipProof) = WabiSabiFactory.CreateCoinWithOwnershipProof(key, inputAmount);

		ConstructionState Build(Money bobValue) =>
			new ConstructionState(parameters)
				.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData())
				.AddOutput(new TxOut(bobValue, bobScript));

		// The size of the coinjoin doesn't depend on the values of its outputs, so the mining fee the coordinator
		// has to leave behind can be calculated with an arbitrary value already registered.
		var sizeToPayFor = Build(parameters.AllowedOutputAmounts.Min).EstimatedVsize + coordinatorScript.EstimateOutputVsize();
		var miningFee = parameters.MiningFeeRate.GetFee(sizeToPayFor) + Money.Satoshis(1);

		return (WabiSabiFactory.CreateRound(parameters), Build(inputAmount - miningFee - anchorValue), coordinatorScript);
	}
}
