using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Storages;

public interface IFilterStorage
{
	Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken);
}
