using System.Collections.Generic;

namespace WalletWasabi.WebClients.ShopWare.Models;

public record Country
(
	string Name,
	bool Active,
	string Id
);

public record CachedCountry
(
	string Id,
	string Name
);

public record GetCountryResponse
(
	List<Country> Elements,
	int Total,
	int Limit
);
