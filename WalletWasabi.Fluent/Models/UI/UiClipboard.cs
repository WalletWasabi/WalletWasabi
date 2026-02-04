using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.UI;

public interface IUiClipboard
{
	Task<string> GetTextAsync();

	Task SetTextAsync(string? text);

	Task ClearAsync();
}

public partial class UiClipboard : IUiClipboard
{
	public async Task<string> GetTextAsync() => await ApplicationHelper.GetTextAsync();

	public async Task SetTextAsync(string? text) => await ApplicationHelper.SetTextAsync(text);

	public async Task ClearAsync() => await ApplicationHelper.ClearAsync();
}
