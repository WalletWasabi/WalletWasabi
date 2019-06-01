using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
	public class FiltersResponse
	{
		public int BestHeight { get; set; }

		[JsonProperty(ItemConverterType = typeof(FilterModelJsonConverter))] // Do not use the deafult jsonifyer, because that's too much data.
		public IEnumerable<FilterModel> Filters { get; set; }
	}
}
