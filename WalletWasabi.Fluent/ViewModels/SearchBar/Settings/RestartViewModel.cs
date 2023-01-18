using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class RestartViewModel : ViewModelBase
{
	public RestartViewModel(string message)
	{
		Message = message;
		RestartCommand = new RelayCommand(() => AppLifetimeHelper.Shutdown(withShutdownPrevention: true, restart: true));
	}

	public string Message { get; }

	public ICommand RestartCommand { get; }
}
