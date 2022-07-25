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
						Content = i
					});

		HasNewContent = contentStream.Select(x => x.IsValid);

		Content = contentStream
			.Where(a => a.IsValid)
			.Select(a => a.Content);
	}

	public IObservable<bool> HasNewContent { get; }
    public IObservable<T> Content { get; }
}
