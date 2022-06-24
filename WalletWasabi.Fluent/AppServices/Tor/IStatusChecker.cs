using System.Collections.Generic;

namespace WalletWasabi.Fluent.AppServices.Tor;

public interface IStatusChecker
{
	IObservable<IList<Issue>> Issues { get; }
}
