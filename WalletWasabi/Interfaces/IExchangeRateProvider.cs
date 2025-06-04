using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;

namespace WalletWasabi.Interfaces;

public interface IExchangeRateProvider
{
	Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken);
}
