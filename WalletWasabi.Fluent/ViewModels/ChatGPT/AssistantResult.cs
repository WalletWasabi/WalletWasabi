using Newtonsoft.Json;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public class AssistantResult
{
	[JsonProperty(PropertyName = "status")]
	public string? Status { get; set; }

	[JsonProperty(PropertyName = "message")]
	public string? Message { get; set; }
}
