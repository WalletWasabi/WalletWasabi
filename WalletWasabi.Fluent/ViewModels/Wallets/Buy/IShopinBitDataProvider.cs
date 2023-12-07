using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IShopinBitDataProvider
{
	Country[] GetCountries();

	Task<WalletWasabi.WebClients.ShopWare.Models.State[]> GetStatesForCountryAsync(
		string countryName,
		CancellationToken cancellationToken);

	Country? GetCurrentCountry();

	void SetCurrentCountry(Country? country);
}
