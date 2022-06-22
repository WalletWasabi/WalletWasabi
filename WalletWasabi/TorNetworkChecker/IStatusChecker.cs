using System.Collections.Generic;

namespace WalletWasabi.TorNetworkChecker;

public interface IStatusChecker
{
	IObservable<IList<Issue>> Issues { get; }
}
