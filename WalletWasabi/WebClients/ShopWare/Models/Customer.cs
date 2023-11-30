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

public record CustomerProfileResponse
(
	ChatField CustomFields,
	string ContextToken,
	string CustomerNumber,
	DateTimeOffset CreatedAt,
	DateTimeOffset? UpdatedAt
);

public record ChatField
(
	string Wallet_Chat_Store
);
