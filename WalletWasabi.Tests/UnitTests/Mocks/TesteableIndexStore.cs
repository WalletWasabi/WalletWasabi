using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Tests.UnitTests.Mocks;

class TesteableIndexStore : IIndexStore
{
	public Func<uint, int, CancellationToken, Task<FilterModel[]>> OnFetchBatchAsync { get; set; }
	public Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken) =>
		OnFetchBatchAsync.Invoke(fromHeight, batchSize, cancellationToken);
}
