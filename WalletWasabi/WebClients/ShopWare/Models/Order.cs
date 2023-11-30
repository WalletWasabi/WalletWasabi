namespace WalletWasabi.WebClients.ShopWare.Models;

public record OrderGenerationResponse
(
	string OrderNumber
);

public record GetOrderListResponse
(
	OrderList Orders
);

public record OrderList
(
	Order[] Elements
);

public record Order
(
	string VersionId,
	DateTimeOffset CreatedAt,
	DateTimeOffset? UpdatedAt,
	StateMachineState StateMachineState,
	string OrderNumber,
	OrderCustomer OrderCustomer,
	string Id
)
{
	public string GetCustomerProfileComment() => (string)OrderCustomer.CustomFields["wallet_chat_store"];
};

public record OrderCustomer
(
	string VersionId,
	DateTimeOffset CreatedAt,
	PropertyBag CustomFields,
	string Id
);

public record StateMachineState
(
	DateTimeOffset CreatedAt,
	string Name,
	string TechnicalName
);


