using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Stores;

public interface IIndexStore
{
	Task<FilterModel[]> FetchBatchAsync(uint fromHeight, int batchSize, CancellationToken cancellationToken);
}
