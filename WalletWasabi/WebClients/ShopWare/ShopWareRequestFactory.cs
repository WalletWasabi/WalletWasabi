using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.WebClients.ShopWare;

public static class ShopWareRequestFactory
{
	public static PropertyBag CustomerRegistrationRequest(string salutationId, string firstName, string lastName, string email, string password, string countryId, string message, string storefrontUrl) =>
		new()
		{
			["salutationId"] = salutationId,
			["firstName"] = firstName,
			["lastName"] = lastName,
			["email"] = email,
			["password"] = password,
			["guest"] = false,
			["affiliateCode"] = "WASABI",
			["acceptedDataProtection"] = true,
			["storefrontUrl"] = storefrontUrl,
			["customFields"] = new PropertyBag { ["wallet_chat_store"] = $"{message}" },
			["billingAddress"] = new PropertyBag
			{
				["street"] = "My street",
				["additionalAddressLine1"] = "My additional address line 1",
				["zipcode"] = "12345",
				["city"] = "Appleton",
				["countryId"] = countryId
			}
		};

	public static PropertyBag CustomerLoginRequest(string email, string password) =>
		new()
		{
			["email"] = email,
			["password"] = password
		};

	public static PropertyBag CustomerProfileUpdateRequest(string firstName, string lastName, string comment) =>
		new()
		{
			["firstName"] = firstName,
			["lastName"] = lastName,
			["customFields"] = new PropertyBag() { ["wallet_chat_store"] = comment }
		};

	public static PropertyBag GetPage(int page, int limit) =>
		new()
		{
			["page"] = page,
			["limit"] = limit
		};

	public static PropertyBag ShoppingCartCreationRequest(string name) =>
		new()
		{
			["name"] = name
		};

	public static PropertyBag ShoppingCartItemsRequest(string productId) =>
		new()
		{
			["items"] = new[]
			{
				new PropertyBag
				{
					["id"] = "0",
					["quantity"] = 1,
					["referencedId"] = productId,
					["type"] = "product"
				}
			}
		};

	public static PropertyBag OrderGenerationRequest() =>
		new()
		{
			["affiliateCode"] = "WASABI",
			["campaignCode"] = "WASABI",
		};

	public static PropertyBag CancelOrderRequest(string orderId) =>
		new()
		{
			["orderId"] = orderId
		};

	public static PropertyBag BillingAddressRequest(string firstName, string lastName, string street, string houseNumber, string zipcode, string city, string stateId, string countryId) =>
		new()
		{
			["firstName"] = firstName,
			["lastName"] = lastName,
			["street"] = street,
			["additionalAddressLine1"] = houseNumber,
			["zipcode"] = zipcode,
			["city"] = city,
			["stateId"] = stateId,
			["countryId"] = countryId
		};

	public static PropertyBag PaymentRequest(string orderId) =>
		new()
		{
			["orderId"] = orderId,
			["finishUrl"] = "",
			["errorUrl"] = ""
		};

	public static PropertyBag GetOrderListRequest() =>
		new()
		{
			["page"] = 1,
			["limit"] = 1,
			["associations"] = new PropertyBag
			{
				["lineItems"] = Array.Empty<object>(),
				["deliveries"] = Array.Empty<object>()
			}
		};
}
