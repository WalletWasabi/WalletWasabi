using System.Collections.Generic;

namespace WalletWasabi.Indexer.Models.Responses;

public class FiltersResponse
{
	public FiltersResponse()
	{
	}

	public FiltersResponse(int bestHeight, FilterModel[] filters)
	{
		BestHeight = bestHeight;
		Filters = filters;
	}

	public int BestHeight { get; set; }

	public IEnumerable<FilterModel> Filters { get; set; } = new List<FilterModel>();
}
