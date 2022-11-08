using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;

namespace WalletWasabi.Fluent.Helpers;

public static class ApplicationUtils
{
	public static IObservable<string> GetClipboard()
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			return clipboard.GetTextAsync().ToObservable().Select(x => x ?? "");
		}

		return Observable.Return("");
	}
}
