using System.Collections.Generic;
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
	string Password,
	bool Guest,
	string AffiliateCode,
	bool AcceptedDataProtection,
	string StorefrontUrl,
	Dictionary<string, string> CustomFields,
	BillingAddress? BillingAddress
);

public record CustomerRegistrationResponse
(
	string Id,
	string CustomerNumber,
	string[] ContextTokens
);

public record CustomerLoginRequest
(
	string Email,
	string Password
);

public record CustomerLoginResponse
(
	string ApiAlias,
	string ContextToken
);
