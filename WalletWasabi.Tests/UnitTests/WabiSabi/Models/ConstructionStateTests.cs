using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

public class MultipartyTransactionStateTests
{
	[Fact]
	public void ConstructionStateFeeRateCalculation()
	{
		var miningFeeRate = new FeeRate(8m);
		var cfg = new WabiSabiConfig();
		var roundParameters = RoundParameters.Create(
				cfg,
				Network.Main,
				miningFeeRate,
				Money.Coins(10));

		var round = WabiSabiFactory.CreateRound(roundParameters);
		var state = round.CoinjoinState;

		var (coin, ownershipProof) = WabiSabiFactory.CreateCoinWithOwnershipProof(
			amount: roundParameters.AllowedInputAmounts.Min + miningFeeRate.GetFee(Constants.P2wpkhInputVirtualSize + Constants.P2wpkhOutputVirtualSize),
			roundId: round.Id);
		state = state.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));
		state = state.AddOutput(new TxOut(roundParameters.AllowedInputAmounts.Min, new Script("0 bf3593d140d512eb607b3ddb5c5ee085f1e3a210")));

		var signingState = state.Finalize();
		Assert.Equal(miningFeeRate, signingState.EffectiveFeeRate);
	}
}
