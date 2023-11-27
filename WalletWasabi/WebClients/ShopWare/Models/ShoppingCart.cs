namespace WalletWasabi.WebClients.ShopWare.Models;

public record ShoppingCartCreationRequest
(
	string Name
);

public record ShoppingCartResponse(string Token);

public record ShoppingCartItem
(
	string Id,
	string ReferencedId,
	string Label,
	int Quantity,
	string? Type,
	bool Good,
	string Description,
	bool Removable,
	bool Stackable,
	bool Modified
);

public record ShoppingCartItemsRequest
(
	ShoppingCartItem[] Items
);


public record Error(string Key, string Level, string Message);

public record Transaction(string PaymentMethodId);

public record Price(float NetPrice, float TotalPrice, float PositionPrice, string TaxStatus);

public record ShoppingCartItemsResponse
(
	string Id,
	Error[] Errors,
	Transaction[] Transactions,
	bool Modified,
	string ApiAlias,
	string Name,
	string Token,
	Price Price,
	ShoppingCartItem[] LineItem,
	string CustomerComment,
	string AffiliateCode,
	string CampaignCode
);

