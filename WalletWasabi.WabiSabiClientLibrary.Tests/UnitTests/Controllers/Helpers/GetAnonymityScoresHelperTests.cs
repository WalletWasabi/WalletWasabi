using NBitcoin;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;

namespace WalletWasabi.WabiSabiClientLibrary.Tests.UnitTests.Controllers.Helpers;

public class GetAnonymityScoresHelperTests
{
	[Fact]
	public void IgnoreWabsabiCoinjoinOutputSortingTest()
	{
		// This test ensures that IsWasabi2Cj does not depend on the order of transaction's outputs. The test is necessary because the format of the GetAnonymityScoresRequest does not preserve the order of the outputs.
		GetAnonymityScoresHelper.AnalyzedTransaction analyzedTransaction = new();
		foreach (var _ in Enumerable.Range(0, 50))
		{
			analyzedTransaction.AddExternalInput();
		}
		// Outputs are not sorted in descending order
		analyzedTransaction.AddExternalOutput(Money.Satoshis(10000), "");
		analyzedTransaction.AddExternalOutput(Money.Satoshis(20000), "");
		analyzedTransaction.AddExternalOutput(Money.Satoshis(10000), "");
		Assert.True(analyzedTransaction.IsWasabi2Cj);
	}
}
