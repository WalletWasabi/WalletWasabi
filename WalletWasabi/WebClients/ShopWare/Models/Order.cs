namespace WalletWasabi.WebClients.ShopWare.Models;

public record OrderGenerationResponse
(
	string Id,
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
	LineItem[] LineItems,
	string Id
);

public record LineItem(float Quantity, string Description, float Price); // TODO: make it real

public record OrderCustomer
(
	string VersionId,
	DateTimeOffset CreatedAt,
	DateTimeOffset? UpdatedAt,
	ChatField CustomFields,
	string Id
);

public record StateMachineState
(
	DateTimeOffset CreatedAt,
	string Name,
	string TechnicalName
);

public record HandlePaymentResponse
(
	string Id
);
