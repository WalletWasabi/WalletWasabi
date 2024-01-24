using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Interfaces;

public interface IExchangeRateProvider
{
	Task<IEnumerable<ExchangeRate>> GetExchangeRateAsync(CancellationToken cancellationToken);
}
