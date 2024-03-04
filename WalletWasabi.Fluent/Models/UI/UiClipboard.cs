using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using Avalonia;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class UiClipboard
{
	public async Task<string> GetTextAsync() => await ApplicationHelper.GetTextAsync();

	public async Task SetTextAsync(string? text) => await ApplicationHelper.SetTextAsync(text);

	public async Task ClearAsync() => await ApplicationHelper.ClearAsync();
}
