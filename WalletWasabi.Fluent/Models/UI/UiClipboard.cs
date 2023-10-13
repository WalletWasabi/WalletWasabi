using System.Threading.Tasks;
using Avalonia;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class UiClipboard
{
	public async Task<string> GetTextAsync()
	{
		if (Application.Current?.Clipboard is { } clipboard)
		{
			return await clipboard.GetTextAsync();
		}
		return await Task.FromResult("");
	}

	public async Task SetTextAsync(string? text)
	{
		if (Application.Current?.Clipboard is { } clipboard && text is { })
		{
			await clipboard.SetTextAsync(text);
			return;
		}
	}

	public async Task ClearAsync()
	{
		if (Application.Current?.Clipboard is { } clipboard)
		{
			await clipboard.ClearAsync();
			return;
		}
	}
}
