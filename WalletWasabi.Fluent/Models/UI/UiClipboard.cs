using System.Threading.Tasks;
using Avalonia;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.UI;

public class UiClipboard : IUiClipboard
{
	public async Task<string?> GetTextAsync()
	{
		if (Application.Current?.Clipboard is { } clipboard)
		{
			return await clipboard.GetTextAsync();
		}
		return await Task.FromResult("");
	}

	public async Task SetTextAsync(string? text)
	{
		if (Application.Current?.Clipboard is { } clipboard)
		{
			await clipboard.SetTextAsync(text);
			return;
		}
		await Task.CompletedTask;
	}

	public async Task ClearAsync()
	{
		if (Application.Current?.Clipboard is { } clipboard)
		{
			await clipboard.ClearAsync();
			return;
		}
		await Task.CompletedTask;
	}
}
