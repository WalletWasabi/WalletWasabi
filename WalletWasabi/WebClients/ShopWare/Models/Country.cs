using System.Collections.Generic;

namespace WalletWasabi.WebClients.ShopWare.Models;
public record GetCountryRequest
(
   int Page,
   int Limit,
   Filter[] Filter
);

public record Filter
(
   string Type,
   string Field,
   string Value
);

public record Translated
(
	string Name,
	Dictionary<string, object> CustomFields,
	List<List<string>> AddressFormat
);

public record CustomerTax
(
	bool Enabled,
	string CurrencyId,
	int Amount,
	string ApiAlias
);

public record CompanyTax
(
	bool Enabled,
	string CurrencyId,
	int Amount,
	string ApiAlias
);

public record Country
(
	Translated Translated,
	DateTime CreatedAt,
	DateTime? UpdatedAt,
	string Name,
	string Iso,
	int Position,
	bool Active,
	bool ShippingAvailable,
	string Iso3,
	bool DisplayStateInRegistration,
	bool ForceStateInRegistration,
	bool CheckVatIdPattern,
	object VatIdPattern,
	bool VatIdRequired,
	CustomerTax CustomerTax,
	CompanyTax CompanyTax,
	object States,
	object Translations,
	bool PostalCodeRequired,
	bool CheckPostalCodePattern,
	bool CheckAdvancedPostalCodePattern,
	object AdvancedPostalCodePattern,
	string DefaultPostalCodePattern,
	List<List<string>> AddressFormat,
	object CustomFields,
	string Id,
	string ApiAlias
);

public record GetCountryResponse
(
	List<Country> Elements,
	List<object> Aggregations,
	int Page,
	int Limit,
	string Entity,
	int Total,
	List<object> States,
	string ApiAlias
);
