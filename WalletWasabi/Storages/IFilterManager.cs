using WalletWasabi.Backend.Models;

namespace WalletWasabi.Storages;

public interface IFilterManager
{
	Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken);
}
