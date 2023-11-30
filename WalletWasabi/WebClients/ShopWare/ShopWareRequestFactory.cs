using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.WebClients.ShopWare;

public static class ShopWareRequestFactory
{
	public static PropertyBag CustomerRegistrationRequest(string firstName, string lastName, string email, string password, string message) =>
		new ()
		{
			["salutationId"] = "018b6635785b70679f479eadf50330f3",
			["firstName"] = firstName,
			["lastName"] = lastName,
			["email"] = email,
			["password"] = password,
			["guest"] = false,
			["affiliateCode"] = "WASABI",
			["acceptedDataProtection"] = true,
			["storefrontUrl"] = "https://wasabi.shopinbit.com",
			["customFields"] = new PropertyBag { ["wallet_chat_store"] = $"||#WASABI#{message}" },
			["billingAddress"] = new PropertyBag
			{
				["street"] = "My street",
				["additionalAddressLine1"] = "My additional address line 1",
				["zipcode"] = "12345",
				["city"] = "Appleton",
				["countryId"] = "5d54dfdc2b384a8e9fff2bfd6e64c186"
			}
		};

	public static PropertyBag CustomerLoginRequest(string email, string password) =>
		new ()
		{
			["email"] = email,
			["password"] = password
		};

	public static PropertyBag CustomerProfileUpdateRequest(string firstName, string lastName, string comment) =>
		new ()
		{
			["firstName"] = firstName,
			["lastName"] = lastName,
			["wallet_chat_store"] = comment
		};

	public static PropertyBag GetPage( int page, int limit) =>
		new ()
		{
			["page"] = page,
			["limit"] = limit
		};

	public static PropertyBag ShoppingCartCreationRequest(string name) =>
		new ()
		{
			["name"] = name
		};

	public static PropertyBag ShoppingCartItemsRequest(string productId) =>
		new ()
		{
			["items"] = new []
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

	public static PropertyBag OrderGenerationRequest()=>
		new ()
		{
			["affiliateCode"] = "WASABI",
			["campaignCode"] = "WASABI",
		};

	public static PropertyBag CancelOrderRequest(string orderId) =>
		new ()
		{
			["orderId"] = orderId
		};

	public static PropertyBag BillingAddressRequest(string street, string houseNumber, string zipcode, string city,
		string countryId) =>
		new()
		{
			["street"] = street,
			["additionalAddressLine1"] = houseNumber,
			["zipcode"] = zipcode,
			["city"] = city,
			["countryId"] = countryId
		};
}
