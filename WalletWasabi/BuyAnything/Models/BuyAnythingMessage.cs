using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.BuyAnything.Models;


public class BuyAnythingMessage
{
	[Required(ErrorMessage = "RequestId is required.")]
	public string RequestId { get; set; }

	[Required(ErrorMessage = "Message is required.")]
	[MinLength(200, ErrorMessage = "Message must be at least 200 characters long.")]
	public string Message { get; set; }
}
