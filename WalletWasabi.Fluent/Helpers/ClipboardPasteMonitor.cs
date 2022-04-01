using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers;

internal class ClipboardPasteMonitor : ReactiveObject
{
	public ClipboardPasteMonitor(IObservable<string> currentTextChanged, Func<string, bool> isValid)
	{
		ClipboardText = Observable
			.Timer(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
			.Repeat()
			.SelectMany(_ => ApplicationUtils.GetClipboardTextAsync())
			.Select(x => x.Trim())
			.DistinctUntilChanged();

		CanPaste = ApplicationUtils.IsMainWindowActive
			.CombineLatest(ClipboardText, currentTextChanged, (isActive, cpText, curAddr) =>
				isActive && isValid(cpText) && !string.Equals(cpText, curAddr));
	}

	public IObservable<string> ClipboardText { get; }

	public IObservable<bool> CanPaste { get; }
}