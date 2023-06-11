using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace WalletWasabi.Fluent.Models.UI;

public class NullClipboard : IClipboard
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

	public Task SetDataObjectAsync(IDataObject data)
	{
		return Task.CompletedTask;
	}

	public Task<string[]> GetFormatsAsync()
	{
		return Task.FromResult(Array.Empty<string>());
	}

	public Task<object> GetDataAsync(string format)
	{
		return Task.FromResult(new object());
	}
}
