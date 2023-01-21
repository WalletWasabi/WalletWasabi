using System.Reactive;

namespace WalletWasabi.Bridge;

public interface IDialogHandler
{
	IObservable<Unit> NotifyError(string title, string message);
}