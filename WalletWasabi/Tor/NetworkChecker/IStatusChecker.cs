using System.Collections.Generic;

namespace WalletWasabi.Tor.NetworkChecker;

public interface IStatusChecker
{
	IObservable<IList<Issue>> Issues { get; }
}
