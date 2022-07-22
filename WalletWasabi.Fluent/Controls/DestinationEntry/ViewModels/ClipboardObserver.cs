using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class ClipboardObserver
{
    public ClipboardObserver()
    {
        ContentChanged = Observable
            .Timer(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
            .Repeat()
            .SelectMany(_ => ApplicationUtils.GetClipboardTextAsync())
            .Select(x => x.Trim())
            .DistinctUntilChanged();
    }

    public IObservable<string> ContentChanged { get; }
}