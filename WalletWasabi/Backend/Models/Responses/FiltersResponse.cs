using System.Collections.Generic;
using Newtonsoft.Json;

namespace WalletWasabi.Backend.Models.Responses
{
	public class FiltersResponse
	{
		public int BestHeight { get; set; }

		public IEnumerable<FilterModel> Filters { get; set; }
	}
}
