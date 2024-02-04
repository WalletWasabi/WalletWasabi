using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using Avalonia;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class UiClipboard
{
	public async Task<string> GetTextAsync() => await ApplicationHelper.GetTextAsync();

	public async Task<string?> TryGetTextAsync()
	{
		try
		{
			return await GetTextAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}

		return null;
	}

	public async Task SetTextAsync(string? text) => await ApplicationHelper.SetTextAsync(text);

	public async Task ClearAsync() => await ApplicationHelper.ClearAsync();
}
