using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.UI;

public class UiClipboard
{
	public async Task<string> GetTextAsync() => await ApplicationHelper.GetTextAsync();

	public async Task SetTextAsync(string? text) => await ApplicationHelper.SetTextAsync(text);

	public async Task ClearAsync() => await ApplicationHelper.ClearAsync();
}
