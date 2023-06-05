using NBitcoin;
using WalletWasabi.Affiliation;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using Xunit;
using System.Linq;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class RoundDataTests
{
	[Fact]
	public void DoNotShareAffiliationOfRemixes()
	{
		var roundData = new RoundData(WabiSabiFactory.CreateRoundParameters(new WabiSabiConfig()));
		var unmixedCoin = WabiSabiFactory.CreateCoin(amount: Money.Coins(1));
		var mixedCoin = WabiSabiFactory.CreateCoin(amount: Money.Coins(3));

		roundData.AddInputCoin(unmixedCoin, isCoordinationFeeExempted: false);
		roundData.AddInputCoin(mixedCoin, isCoordinationFeeExempted: true);
		roundData.AddInputAffiliationId(unmixedCoin, "bluewallet");
		roundData.AddInputAffiliationId(mixedCoin, "bluewallet");

		var cj = Network.Main.CreateTransaction();
		cj.Inputs.Add(unmixedCoin.Outpoint);
		cj.Inputs.Add(mixedCoin.Outpoint);
		cj.Outputs.Add(Money.Coins(2.9999M), BitcoinFactory.CreateScript());

		var transactionData = roundData.FinalizeRoundData(cj);
		var txNotificationForBlueWallet = transactionData.GetAffiliationData("bluewallet", cj.GetHash());
		var reportedAffiliatedCoin = Assert.Single(txNotificationForBlueWallet.Inputs.Where(x => x.IsAffiliated));

		Assert.Equal(unmixedCoin.Outpoint.Hash.ToBytes(), reportedAffiliatedCoin.Prevout.Hash);
		Assert.Equal(unmixedCoin.Outpoint.N, reportedAffiliatedCoin.Prevout.Index);

		var txNotificationForUnknownWallet = transactionData.GetAffiliationData("unknown", cj.GetHash());
		Assert.Empty(txNotificationForUnknownWallet.Inputs.Where(x => x.IsAffiliated));
	}
}
