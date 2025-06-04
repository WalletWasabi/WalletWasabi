using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;

namespace WalletWasabi.Stores;

public interface IIndexStore
{
	Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken);
}
