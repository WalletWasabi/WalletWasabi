namespace WalletWasabi.WebClients.ShopWare.Models;

public record CustomerRegistrationResponse
(
	string Id,
	string CustomerNumber,
	string[] ContextTokens
);

public record CustomerLoginResponse
(
	string ContextToken
);


public record LocalCustomer(string Id, string CustomerNumber, string Email,
	string Password, string LastKnownAccessToken)
{
	public string LastKnownAccessToken { get; set; } = LastKnownAccessToken;

	public static readonly LocalCustomer Empty = new(string.Empty, string.Empty,
		string.Empty, string.Empty, string.Empty);
}

