using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.UI;

public class NullClipboard : IUiClipboard
{
	public Task<string?> GetTextAsync()
	{
		return Task.FromResult<string?>("");
	}

	public Task SetTextAsync(string? text)
	{
		return Task.CompletedTask;
	}

	public Task ClearAsync()
	{
		return Task.CompletedTask;
	}
}
