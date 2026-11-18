using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class TransactionStoreTests
{
	[Fact]
	public async Task MergedTransactionIsPersistedAsync()
	{
		var workDir = await Common.GetEmptyWorkDirAsync();
		var tx = Network.RegTest.CreateTransaction();
		tx.Inputs.Add(new OutPoint(RandomUtils.GetUInt256(), 0));
		tx.Outputs.Add(Money.Coins(1), Script.Empty);

		using (var store = new TransactionStore(workDir, Network.RegTest))
		{
			await store.InitializeAsync(CancellationToken.None);
			Assert.True(store.TryAdd(new SmartTransaction(tx, Height.Mempool, labels: new LabelsArray("a"))));
			Assert.True(store.TryAddOrUpdate(new SmartTransaction(tx, Height.Mempool, labels: new LabelsArray("b"))));
			Assert.True(store.TryUpdate(new SmartTransaction(tx, Height.Mempool, labels: new LabelsArray("c"))));
		}

		using var reloaded = new TransactionStore(workDir, Network.RegTest);
		await reloaded.InitializeAsync(CancellationToken.None);

		Assert.True(reloaded.TryGetTransaction(tx.GetHash(), out var stored));
		Assert.Equal(new LabelsArray("a", "b", "c"), stored.Labels);
	}
}
