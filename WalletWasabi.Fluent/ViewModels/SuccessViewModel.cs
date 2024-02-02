using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels;

[NavigationMetaData(Title = "Success")]
public partial class SuccessViewModel : RoutableViewModel
{
	private SuccessViewModel()
	{
		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private async Task OnNextAsync()
	{
		await Task.Delay(UiConstants.CloseSuccessDialogMillisecondsDelay);

		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (NextCommand is not null && NextCommand.CanExecute(default))
		{
			NextCommand.Execute(default);
		}
	}
}
