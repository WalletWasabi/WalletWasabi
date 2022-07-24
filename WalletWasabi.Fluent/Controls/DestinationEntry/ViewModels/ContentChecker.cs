using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class ContentChecker<T>
{
	public ContentChecker(IObservable<T> incoming, IObservable<T> current, Func<T, bool> isValid)
	{
		var contentStream =
			incoming
				.CombineLatest(
					current,
					(i, c) => new
					{
						IsValid = isValid(i) &&
						          !Equals(i, c),
						NewContent = i
					});

		var activatedContentStream = ApplicationUtils.IsMainWindowActive.CombineLatest(contentStream)
			.Where(a => a.First)
			.Select(a => a.Second);

		HasNewContent = activatedContentStream.Select(x => x.IsValid);

		NewContent = activatedContentStream
			.Where(a => a.IsValid)
			.Select(a => a.NewContent);
	}

	public IObservable<bool> HasNewContent { get; }
    public IObservable<T> NewContent { get; }
}
