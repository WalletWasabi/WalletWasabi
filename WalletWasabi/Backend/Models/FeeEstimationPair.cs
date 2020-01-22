using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models
{
	/// <summary>
	/// Satoshi per byte.
	/// </summary>
	public class FeeEstimationPair
	{
		private static readonly int testStaticReadonlyField;

		[Required]
		public long Economical { get; set; }

		[Required]
		public long Conservative { get; set; }
	}
}
