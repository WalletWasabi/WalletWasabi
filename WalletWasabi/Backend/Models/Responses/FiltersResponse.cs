using System.Collections.Generic;

namespace WalletWasabi.Backend.Models.Responses;

public class FiltersResponse
{
	public FiltersResponse(uint bestHeight, FilterModel[] filters)
	{
		BestHeight = bestHeight;
		Filters = filters;
	}

	public uint BestHeight { get; set; }

	public IEnumerable<FilterModel> Filters { get; set; } = new List<FilterModel>();
}
