using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.Tests.UnitTests;

public class MockShopWareApiClient : IShopWareApiClient
{
	public Func<string, PropertyBag, Task<CustomerRegistrationResponse>>? OnRegisterCustomerAsync { get; set; }
	public Func<string, PropertyBag, Task<CustomerLoginResponse>>? OnLoginCustomerAsync { get; set; }
	public Func<string, PropertyBag, Task<PropertyBag>>? OnUpdateCustomerProfileAsync { get; set; }
	public Func<string, PropertyBag, Task<PropertyBag>>? OnUpdateCustomerBillingAddressAsync { get; set; }
	public Func<string, Task<CustomerProfileResponse>>? OnGetCustomerProfileAsync { get; set; }
	public Func<string, PropertyBag, Task<ShoppingCartResponse>>? OnGetOrCreateShoppingCartAsync { get; set; }
	public Func<string, PropertyBag, Task<ShoppingCartItemsResponse>>? OnAddItemToShoppingCartAsync { get; set; }
	public Func<string, PropertyBag, Task<OrderGenerationResponse>>? OnGenerateOrderAsync { get; set; }
	public Func<string, PropertyBag, Task<GetOrderListResponse>>? OnGetOrderListAsync { get; set; }
	public Func<string, PropertyBag, Task<StateMachineState>>? OnCancelOrderAsync { get; set; }
	public Func<string, PropertyBag, Task<GetCountryResponse>>? OnGetCountriesAsync { get; set; }
	public Func<string, string, Task<GetStateResponse>>? OnGetStatesByCountryIdAsync { get; set; }
	public Func<string, PropertyBag, Task<HandlePaymentResponse>>? OnHandlePaymentAsync { get; set; }

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnRegisterCustomerAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("RegisterCustomerAsync is not implemented.");

	public Task<CustomerLoginResponse> LoginCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnLoginCustomerAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("LoginCustomerAsync is not implemented.");

	public Task<PropertyBag> UpdateCustomerProfileAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnUpdateCustomerProfileAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("UpdateCustomerProfileAsync is not implemented.");

	public Task<PropertyBag> UpdateCustomerBillingAddressAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnUpdateCustomerBillingAddressAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("UpdateCustomerBillingAddressAsync is not implemented.");

	public Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnGetOrCreateShoppingCartAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("GetOrCreateShoppingCartAsync is not implemented.");

	public Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnAddItemToShoppingCartAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("AddItemToShoppingCartAsync is not implemented.");

	public Task<OrderGenerationResponse> GenerateOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnGenerateOrderAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("GenerateOrderAsync is not implemented.");

	public Task<GetOrderListResponse> GetOrderListAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnGetOrderListAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("GetOrderListAsync is not implemented.");

	public Task<StateMachineState> CancelOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnCancelOrderAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("CancelOrderAsync is not implemented.");

	public Task<GetCountryResponse> GetCountriesAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnGetCountriesAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("GetCountriesAsync is not implemented.");

	public Task<GetStateResponse> GetStatesByCountryIdAsync(string ctxToken, string countryId, CancellationToken cancellationToken) =>
		OnGetStatesByCountryIdAsync?.Invoke(ctxToken, countryId)
		?? throw new NotImplementedException("GetStatesByCountryIdAsync is not implemented.");

	public Task<HandlePaymentResponse> HandlePaymentAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		OnHandlePaymentAsync?.Invoke(ctxToken, request)
		?? throw new NotImplementedException("HandlePaymentAsync is not implemented.");

	public Task<CustomerProfileResponse> GetCustomerProfileAsync(string ctxToken, CancellationToken cancellationToken) =>
		OnGetCustomerProfileAsync?.Invoke(ctxToken)
		?? throw new NotImplementedException("GetCustomerProfileAsync is not implemented.");
}
