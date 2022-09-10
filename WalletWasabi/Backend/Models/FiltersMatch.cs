using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models;

public class FiltersMatch
{
	[Required]
	public bool HasMatched { get; set; }

	[Required]
	public List<FilterModel> BufferFilters { get; } = new();
}
