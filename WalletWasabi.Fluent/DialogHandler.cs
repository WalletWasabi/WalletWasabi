using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Bridge;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public class DialogHandler : IDialogHandler
{
	public IObservable<Unit> NotifyError(string title, string message)
	{
		return Observable.FromAsync(() => ShowErrorAsync(title, message, "", NavigationTarget.DialogScreen));
	}

	public static async Task NavigateDialogAsync<TResult>(DialogViewModelBase<TResult> dialog, NavigationTarget target, NavigationMode navigationMode = NavigationMode.Normal)
	{
		var dialogTask = dialog.GetDialogResultAsync();

		Navigate(target).To(dialog, navigationMode);

		var result = await dialogTask;

		Navigate(target).Back();
	}

	private async Task ShowErrorAsync(string title, string message, string caption, NavigationTarget target = NavigationTarget.Default)
	{
		var dialog = new ShowErrorDialogViewModel(message, title, caption);
		await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);
	}

	private static INavigationStack<RoutableViewModel> Navigate(NavigationTarget currentTarget)
	{
		return currentTarget switch
		{
			NavigationTarget.HomeScreen => NavigationState.Instance.HomeScreenNavigation,
			NavigationTarget.DialogScreen => NavigationState.Instance.DialogScreenNavigation,
			NavigationTarget.FullScreen => NavigationState.Instance.FullScreenNavigation,
			NavigationTarget.CompactDialogScreen => NavigationState.Instance.CompactDialogScreenNavigation,
			_ => throw new NotSupportedException(),
		};
	}
}
