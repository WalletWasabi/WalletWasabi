using Newtonsoft.Json;

namespace WalletWasabi.WebClients.ShopWare.Models;

public record BillingAddress
(
	string Street,
	string AdditionalAddressLine1,
	string Zipcode,
	string City,
	string CountryId
);

public record CustomerRegistrationRequest
(
	string SalutationId,
	string FirstName,
	string LastName,
	string Email,
	bool Guest,
	string AffiliateCode,
	bool AcceptedDataProtection,
	string StorefrontUrl,
	BillingAddress? BillingAddress
);

public record CustomerRegistrationResponse
(
	string Id,
	string CustomerNumber,
	string[] ContextTokens
);
