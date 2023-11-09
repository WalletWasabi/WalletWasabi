using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "Rename Wallet")]
public partial class WalletRenameViewModel : DialogViewModelBase<Unit>
{
	[AutoNotify] private string _newWalletName;

	private WalletRenameViewModel(IWalletModel wallet)
	{
		_newWalletName = wallet.Name;

		this.ValidateProperty(
			x => x.NewWalletName,
			errors =>
			{
				if (ErrorString() is { } error)
				{
					errors.Add(ErrorSeverity.Error, error);
				}
			});

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		var canRename = this.WhenAnyValue(model => model.NewWalletName, selector: newName => !Equals(newName, wallet.Name) && !Validations.Any);
		NextCommand = ReactiveCommand.Create(() => OnRename(wallet), canRename);
	}

	private string? ErrorString()
	{
		return NewWalletName switch
		{
			{ Length: 0 } => "The name cannot be empty",
			{ Length: > 50 } => "The name is too long",
			var name when name.TrimStart().Length != name.Length => "The name should not have leading white spaces",
			var name when name.TrimEnd().Length != name.Length => "The name should not have trailing white spaces",
			_ => null
		};
	}
	
	private void OnRename(IWalletModel wallet)
	{
		try
		{
			wallet.Name = NewWalletName;
			Close();
		}
		catch
		{
			UiContext.Navigate().To().ShowErrorDialog($"The wallet cannot be renamed to {NewWalletName}", "Invalid name", "Cannot rename the wallet", NavigationTarget.CompactDialogScreen);
		}
	}
}
