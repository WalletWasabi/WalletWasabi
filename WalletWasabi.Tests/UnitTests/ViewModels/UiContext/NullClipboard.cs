using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace WalletWasabi.Fluent.Models.UI;

public class NullClipboard : IUiClipboard
{
	public Task<string> GetTextAsync()
	{
		return Task.FromResult("");
	}

	public Task SetTextAsync(string text)
	{
		return Task.CompletedTask;
	}

	public Task ClearAsync()
	{
		return Task.CompletedTask;
	}
}
