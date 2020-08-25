using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models
{
	public class ExchangeRate
	{
		[Required]
		public string Ticker { get; set; } = null!;

		[Required]
		public decimal Rate { get; set; }
	}
}
