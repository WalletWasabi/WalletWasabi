using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "Rename Wallet")]
public partial class WalletRenameViewModel : DialogViewModelBase<Unit>
{
	private readonly WalletViewModelBase _walletViewModelBase;
	[AutoNotify] private string _walletName = string.Empty;

	public WalletRenameViewModel(WalletViewModelBase walletViewModelBase)
	{
		_walletViewModelBase = walletViewModelBase;

		WalletName = _walletViewModelBase.WalletName;

		var canExecute =
			this.WhenAnyValue(x => x.WalletName)
 				.Select(x => !string.IsNullOrWhiteSpace(x) && !Validations.Any);

		NextCommand = ReactiveCommand.Create(OnNext, canExecute);

		this.ValidateProperty(x => x.WalletName, ValidateWalletName);
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private void OnNext()
	{
		_walletViewModelBase.WalletName = WalletName;
		Close(DialogResultKind.Normal, Unit.Default);
	}

	private void ValidateWalletName(IValidationErrors errors)
	{
		var error = WalletHelpers.ValidateWalletName(WalletName);
		if (error is { } e)
		{
			errors.Add(e.Severity, e.Message);
		}
	}
}
