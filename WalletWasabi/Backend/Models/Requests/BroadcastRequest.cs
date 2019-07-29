using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models.Requests
{
	public class BroadcastRequest
	{
		[Required]
		public string Hex { get; set; }
	}
}
