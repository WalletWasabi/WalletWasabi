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
	string AmountTotal,
	StateMachineState StateMachineState,
	Deliveries[] Deliveries,
	string OrderNumber,
	OrderCustomer OrderCustomer,
	LineItem[] LineItems,
	string Id,
	OrderCustomFields? CustomFields,
	string Btcpay_PaymentLink,
	ShippingCosts ShippingCosts
);

public record ShippingCosts
(
	string TotalPrice
);
public record Deliveries
(
	string OrderId,
	string[] TrackingCodes,
	StateMachineState StateMachineState,
	ShippingCosts ShippingCosts
);
public record OrderCustomFields
(
	string Concierge_Request_Status_State,
	string Concierge_Request_Attachements_Links,
	string Btcpay_PaymentLink,
	string Btcpay_Destination,
	string Btcpay_Amount,
	string Btcpay_Rate,
	string BtcpayOrderStatus,
	bool PaidAfterExpiration,
	bool Overpaid
);

public record LineItem
(
	float Quantity,
	string Label,
	float UnitPrice,
	float TotalPrice
);

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
