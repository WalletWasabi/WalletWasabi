using System.Reactive;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ReactiveUI;
using WalletWasabi.Bridge;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class HardwareWalletViewModel : ViewModelBase
{
	[AutoNotify] private string _errorMessage = "";

	public HardwareWalletViewModel(IAddress address, IHwiClient hwiClient)
	{
		ShowOnHwWalletCommand = ReactiveCommand
			.CreateFromObservable(() => hwiClient.Show(address));

		ShowOnHwWalletCommand
			.Where(x => x.IsFailure)
			.Select(x => x.Error)
			.BindTo(this, x => x.ErrorMessage);

		ShowOnHwWalletCommand
			.IsExecuting
			.Where(b => b)
			.Do(_ => ErrorMessage = "")
			.Subscribe();

		IsBusy = ShowOnHwWalletCommand.IsExecuting;
	}

	public IObservable<bool> IsBusy { get; }

	public ReactiveCommand<Unit, Result> ShowOnHwWalletCommand { get; }
}
