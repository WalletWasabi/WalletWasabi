using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.BlockFilters;

namespace WalletWasabi.Stores;

public interface IFilterStore
{
	Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken);
}
