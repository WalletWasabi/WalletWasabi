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

