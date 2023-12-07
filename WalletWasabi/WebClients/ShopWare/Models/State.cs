using System.Collections.Generic;

namespace WalletWasabi.WebClients.ShopWare.Models;

public record GetStateResponse
(
	List<State> Elements,
	int Total,
	int Limit
);

public record State
(
	string Name,
	string CountryId,
	string ShortCode,
	bool Active,
	string Id
);
