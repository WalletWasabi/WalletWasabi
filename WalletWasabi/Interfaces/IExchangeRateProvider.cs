using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Interfaces
{
	public interface IExchangeRateProvider
	{
		Task<List<ExchangeRate>> GetExchangeRateAsync();
	}
}
