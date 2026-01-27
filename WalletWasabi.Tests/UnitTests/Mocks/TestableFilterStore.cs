using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Stores;

namespace WalletWasabi.Tests.UnitTests.Mocks;

class TestableFilterStore : IFilterStore
{
	public required Func<uint, int, CancellationToken, Task<FilterModel[]>> OnFetchBatchAsync { get; init; }
	public Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken) =>
		OnFetchBatchAsync.Invoke(fromHeight, batchSize, cancellationToken);
}
