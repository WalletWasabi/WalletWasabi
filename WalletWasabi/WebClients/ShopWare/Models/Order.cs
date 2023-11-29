using System.Collections.Generic;

namespace WalletWasabi.WebClients.ShopWare.Models;


public record OrderGenerationRequest
(
	string CustomerComment,
	string AffiliateCode,
	string CampaignCode
);

public record CalculatedTax
(
	float Tax,
	int TaxRate,
	float Price,
	string ApiAlias
);

public record TaxRule
(
	int TaxRate,
	int Percentage,
	string ApiAlias
);

public record OrderPrice
(
	float NetPrice,
	float TotalPrice,
	CalculatedTax[] CalculatedTaxes,
	TaxRule[] TaxRules,
	float PositionPrice,
	string TaxStatus,
	string ApiAlias
);

public record LineItem
(
	string Label,
	OrderPrice Price,
	string ApiAlias
);

public record OrderGenerationResponse
(
	string OrderNumber,
	OrderPrice Price,
	LineItem[] LineItems,
	string CustomerComment,
	string AffiliateCode,
	string CampaignCode,
	string ApiAlias
);


public record GetOrderListResponse
(
	string ApiAlias,
	OrderList Orders,
	object[] PaymentChangeable
);

public record OrderList
(
	Order[] Elements,
	object[] Aggregations,
	int Page,
	int Limit,
	string Entity,
	int Total,
	object[] States,
	string ApiAlias
);

public record Order
(
	Extensions Extensions,
	string VersionId,
	object[] Translated,
	DateTimeOffset CreatedAt,
	DateTimeOffset? UpdatedAt,
	StateMachineState StateMachineState,
	string OrderNumber,
	string CurrencyId,
	float CurrencyFactor,
	string SalesChannelId,
	string BillingAddressId,
	string BillingAddressVersionId,
	DateTimeOffset OrderDateTime,
	DateTimeOffset OrderDate,
	OrderPrice Price,
	int AmountTotal,
	int AmountNet,
	int PositionPrice,
	string TaxStatus,
	ShippingCosts ShippingCosts,
	int ShippingTotal,
	OrderCustomer OrderCustomer,
	object Currency,
	string LanguageId,
	object Language,
	object Addresses,
	BillingAddress BillingAddress,
	object Deliveries,
	object LineItems,
	object Transactions,
	string DeepLinkCode,
	object[] Documents,
	object Tags,
	string AffiliateCode,
	string CampaignCode,
	string CustomerComment,
	object CreatedById,
	object UpdatedById,
	object Source,
	object CustomFields,
	string Id,
	string ApiAlias
);

public record OrderCustomer
(
	string VersionId,
	object[] Translated,
	DateTimeOffset CreatedAt,
	object UpdatedAt,
	string Email,
	string SalutationId,
	string FirstName,
	string LastName,
	object Title,
	object VatIds,
	object Company,
	string CustomerNumber,
	object Salutation,
	object CustomFields,
	string Id,
	string ApiAlias
);

public record Extensions
(
	Search Search,
	string VersionId,
	object[] Translated,
	int AutoIncrement,
	string OrderNumber
);

public record Search
(
	object[] Extensions,
	object UniqueIdentifier,
	object[] Translated,
	int AutoIncrement,
	string OrderNumber
);

public record ShippingCosts
(
    int UnitPrice,
    int Quantity,
    int TotalPrice,
    object[] CalculatedTaxes,
    object[] TaxRules
);


public record StateMachineState
(
	TranslatedState Translated,
	DateTimeOffset CreatedAt,
	object UpdatedAt,
	string Name,
	string TechnicalName,
	object CustomFields,
	string ApiAlias
);

public record TranslatedState
(
	string Name,
	object CustomFields
);

public record CancelOrderRequest(string OrderId);

public record UpdateOrderRequest
(
	string OrderId,
	string CustomerComment
);

public class GetOrderListRequest
{
	public GetOrderListRequest(DateTimeOffset lastUpdate)
	{
		Parameters = new() { { "gt", lastUpdate } };
	}

	public string Type { get; } = "range";
	public string Field { get; } = "updatedAt";
	public Dictionary<string, DateTimeOffset> Parameters { get; }
}
