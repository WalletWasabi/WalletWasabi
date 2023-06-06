using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Features.Onboarding.Pages;

public class FourthViewModel : ViewModelBase, IWizardPage
{
	public FourthViewModel(Func<Task<bool>> onCreate, Func<Task<bool>> onConnectHandwareWallet, Func<Task<bool>> onImport, Func<Task<bool>> onRecover)
	{
		var createNew = ReactiveCommand.CreateFromTask(onCreate);

		CreateWalletCommand = createNew;
		var connectHardwareWalletCommand = ReactiveCommand.CreateFromTask(onConnectHandwareWallet);
		ConnectHardwareWalletCommand = connectHardwareWalletCommand;
		var importWalletCommand = ReactiveCommand.CreateFromTask(onImport);
		ImportWalletCommand = importWalletCommand;
		var recoverWalletCommand = ReactiveCommand.CreateFromTask(onRecover);
		RecoverWalletCommand = recoverWalletCommand;
		IsValid = createNew.Merge(importWalletCommand).Merge(recoverWalletCommand).Merge(connectHardwareWalletCommand);
	}

	public IObservable<bool> IsValid { get; }
	public string NextText => "Continue";
	public bool ShowNext => false;

	public ICommand CreateWalletCommand { get; }
	public ICommand ConnectHardwareWalletCommand { get; }
	public ICommand ImportWalletCommand { get; }
	public ICommand RecoverWalletCommand { get; }
}
