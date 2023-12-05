using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.WebClients.ShopWare;

public interface IShopWareApiClient
{
	Task<CustomerRegistrationResponse> RegisterCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<CustomerLoginResponse> LoginCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<PropertyBag> UpdateCustomerProfileAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<PropertyBag> UpdateCustomerBillingAddressAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<CustomerProfileResponse> GetCustomerProfileAsync(string ctxToken, CancellationToken cancellationToken);

	Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<OrderGenerationResponse> GenerateOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<GetOrderListResponse> GetOrderListAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<StateMachineState> CancelOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<GetCountryResponse> GetCountriesAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);

	Task<GetStateResponse> GetStatesByCountryIdAsync(string ctxToken, string countryId, CancellationToken cancellationToken);

	Task<HandlePaymentResponse> HandlePaymentAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken);
}
