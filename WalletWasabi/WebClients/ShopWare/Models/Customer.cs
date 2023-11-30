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

