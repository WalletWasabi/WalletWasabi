using System.Collections.Generic;

namespace WalletWasabi.Backend.Models.Responses
{
	public class FiltersResponse
	{
		public int BestHeight { get; set; }

		public IEnumerable<string> Filters { get; set; }
	}
}
