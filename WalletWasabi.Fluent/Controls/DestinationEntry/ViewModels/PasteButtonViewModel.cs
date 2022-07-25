using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class PasteButtonViewModel : IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public PasteButtonViewModel(IObservable<string> incomingContent, ContentChecker<string> contentChecker, IObservable<bool> isMainWindowActive)
	{
		HasNewContent = isMainWindowActive.CombineLatest(contentChecker.HasNewContent, (isActive, hasNewContent) => isActive && hasNewContent);

		PasteCommand = ReactiveCommand
			.CreateFromObservable(() => incomingContent.Take(1))
			.DisposeWith(_disposables);

		HasNewContent = isMainWindowActive.CombineLatest(
			contentChecker.HasNewContent,
			(isActive, hasNewContent) => isActive && hasNewContent);
	}

	public ReactiveCommand<Unit, string> PasteCommand { get; }

	public IObservable<bool> HasNewContent { get; }

	public void Dispose()
	{
		_disposables.Dispose();
		PasteCommand.Dispose();
	}
}
