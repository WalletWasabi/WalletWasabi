using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Helpers;

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
	}

	public IObservable<bool> HasNewContent { get; }
}
