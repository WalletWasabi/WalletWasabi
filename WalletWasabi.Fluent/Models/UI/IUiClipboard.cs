using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.UI;

public interface IUiClipboard
{
	Task<string?> GetTextAsync();

	Task SetTextAsync(string? text);

	Task ClearAsync();
}
