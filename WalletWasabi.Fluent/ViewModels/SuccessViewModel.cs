using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels;

[NavigationMetaData(Title = "Success")]
public partial class SuccessViewModel : RoutableViewModel
{
	private SuccessViewModel(string successText)
	{
		SuccessText = successText;
		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string SuccessText { get; }

	private async Task OnNextAsync()
	{
		await Task.Delay(500);

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
