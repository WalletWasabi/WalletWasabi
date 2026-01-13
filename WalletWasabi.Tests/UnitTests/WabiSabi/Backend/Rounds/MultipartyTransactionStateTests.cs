using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds;

public class MultipartyTransactionStateTests
{
	[Fact]
	public void CanGetDifferentialStateTest()
	{
		var cfg = new WabiSabiConfig();
		var round = WabiSabiFactory.CreateRound(cfg);

		var commitmentData = WabiSabiFactory.CreateCommitmentData(round.Id);

		(var coin1, var ownershipProof1) = WabiSabiFactory.CreateCoinWithOwnershipProof(roundId: round.Id);
		(var coin2, var ownershipProof2) = WabiSabiFactory.CreateCoinWithOwnershipProof(roundId: round.Id);
		(var coin3, var ownershipProof3) = WabiSabiFactory.CreateCoinWithOwnershipProof(roundId: round.Id);

		// Three events / three states
		var state0 = round.Assert<ConstructionState>();
		var state1 = state0.AddInput(coin1, ownershipProof1, commitmentData);
		var state2 = state1.AddInput(coin2, ownershipProof2, commitmentData);
		var state3 = state2.AddInput(coin3, ownershipProof3, commitmentData);

		// Unknown state. Assumes full state is required
		var diffd30 = state3.GetStateFrom(-1);
		Assert.Equal(state3.Inputs, diffd30.Inputs);
		Assert.Equal(state3.Outputs, diffd30.Outputs);

		// Only one event is missing
		var diffd32 = state3.GetStateFrom(3);
		var input = Assert.Single(diffd32.Inputs);
		Assert.Equal(coin3.Outpoint, input.Outpoint);

		// Two events are missing
		var diffd31 = state3.GetStateFrom(2);
		Assert.Collection(
			diffd31.Inputs,
			x => Assert.Equal(coin2.Outpoint, x.Outpoint),
			x => Assert.Equal(coin3.Outpoint, x.Outpoint));

		// No event is missing (already updated)
		var diffd33 = state3.GetStateFrom(4);
		Assert.Empty(diffd33.Inputs);
		Assert.Empty(diffd33.Outputs);

		// Merge initial state0 with full diff. Expected to get state3
		var merged03 = state0.Merge(diffd30);
		Assert.Equal(state3.Inputs, merged03.Inputs);
		Assert.Equal(state3.Outputs, merged03.Outputs);

		// Merge state1 with diff between 1 and 3. Expected to get state3
		var merged13 = state1.Merge(diffd31);
		Assert.Equal(state3.Inputs, merged13.Inputs);
		Assert.Equal(state3.Outputs, merged13.Outputs);

		var diff00 = state0.GetStateFrom(0);
		var diff10 = state1.GetStateFrom(0);
		var diff21 = state2.GetStateFrom(1);
		var diff32 = state3.GetStateFrom(2);
		var clientState1 = state1;
		var clientState3 = state3.GetStateFrom(2).AddPreviousStates(clientState1, round.Id);
		Assert.Equal(state3.Inputs, clientState3.Inputs);
		Assert.Equal(state3.Outputs, clientState3.Outputs);
		Assert.Equal(clientState3.Inputs, state3.Inputs);
		Assert.Equal(clientState3.Outputs, state3.Outputs);
	}

	[Fact]
	public void MaxSuggestedSteppingTest()
	{
		WabiSabiConfig config = new()
		{
			MaxSuggestedAmountBase = Money.Coins(0.1m)
		};

		RoundParameterFactory roundParameterFactory = new(config, Network.Main);
		MaxSuggestedAmountProvider maxSuggestedAmountProvider = new(config);
		RoundParameters parameters = roundParameterFactory.CreateRoundParameter(new FeeRate(12m), maxSuggestedAmountProvider.MaxSuggestedAmount);
		Round roundLargest = new(parameters, SecureRandom.Instance);

		// First Round is the largest.
		Assert.Equal(Money.Satoshis(ProtocolConstants.MaxAmountPerAlice), roundLargest.Parameters.MaxSuggestedAmount);

		// Simulate 63 successful rounds.
		Dictionary<Money, int> histogram = new();
		for (int i = 0; i < 63; i++)
		{
			maxSuggestedAmountProvider.StepMaxSuggested(roundLargest, true);
			parameters = roundParameterFactory.CreateRoundParameter(new FeeRate(12m), maxSuggestedAmountProvider.MaxSuggestedAmount);
			Round round = new(parameters, SecureRandom.Instance);

			var maxSuggested = round.Parameters.MaxSuggestedAmount;

			if (!histogram.TryGetValue(maxSuggested, out int value))
			{
				histogram.Add(maxSuggested, 1);
			}
			else
			{
				histogram[maxSuggested] = value + 1;
			}
		}

		// Check the distribution of MaxSuggestedAmounts.
		Assert.Equal(1, histogram[Money.Coins(10_000)]);
		Assert.Equal(2, histogram[Money.Coins(1000)]);
		Assert.Equal(4, histogram[Money.Coins(100)]);
		Assert.Equal(8, histogram[Money.Coins(10)]);
		Assert.Equal(16, histogram[Money.Coins(1)]);
		Assert.Equal(32, histogram[Money.Coins(0.1m)]);

		// Simulate many unsuccessful input-reg. At the end we should always stick with the largest again.
		for (int i = 0; i < 2; i++)
		{
			maxSuggestedAmountProvider.StepMaxSuggested(roundLargest, false);
			Assert.Equal(Money.Satoshis(ProtocolConstants.MaxAmountPerAlice), maxSuggestedAmountProvider.MaxSuggestedAmount);
		}

		// Finally one successful round.
		maxSuggestedAmountProvider.StepMaxSuggested(roundLargest, true);
		Assert.Equal(Money.Satoshis(ProtocolConstants.MaxAmountPerAlice), maxSuggestedAmountProvider.MaxSuggestedAmount);

		maxSuggestedAmountProvider.StepMaxSuggested(roundLargest, true);
		Assert.Equal(Money.Coins(0.1m), maxSuggestedAmountProvider.MaxSuggestedAmount);

		RoundParameters blameParameters = roundParameterFactory.CreateBlameRoundParameter(new FeeRate(12m), roundLargest) with
		{
			MinInputCountByRound = config.MinInputCountByBlameRound
		};

		BlameRound blameRound = new(blameParameters, roundLargest, new HashSet<OutPoint>(), SecureRandom.Instance);

		// Blame rounds never change the MaxSuggestedAmount.
		for (int i = 0; i < 2; i++)
		{
			maxSuggestedAmountProvider.StepMaxSuggested(blameRound, true);
			Assert.Equal(Money.Coins(0.1m), maxSuggestedAmountProvider.MaxSuggestedAmount);
		}
	}
}
