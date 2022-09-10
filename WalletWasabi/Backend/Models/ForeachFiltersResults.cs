using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models;

public class ForeachFiltersResults
{
	[Required]
	public bool HasMatched { get; set; }

	[Required]
	public List<FilterModel> BufferFiltersRead { get; } = new();
}
