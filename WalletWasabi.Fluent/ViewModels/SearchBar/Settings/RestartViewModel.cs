using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class RestartViewModel: ViewModelBase
{
	public string Message { get; }

	public RestartViewModel(string message)
	{
		Message = message;
		RestartCommand = ReactiveCommand.Create(AppLifetimeHelper.Restart);
	}

	public ReactiveCommand<Unit, Unit> RestartCommand { get; }
}