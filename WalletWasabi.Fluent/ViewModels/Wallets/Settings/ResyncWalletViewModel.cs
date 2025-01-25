using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(Title = "Resync Wallet", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ResyncWalletViewModel : DialogViewModelBase<int?>
{
	[AutoNotify] private string _startingHeight = "";

	public ResyncWalletViewModel()
	{
		this.ValidateProperty(x => x.StartingHeight, ValidateStartingHeight);

		SetupCancel(false, true, true);

		NextCommand = ReactiveCommand.Create(
			() =>
				Close(DialogResultKind.Normal, StartingHeight is "" ? 0 : int.Parse(StartingHeight)),
			this.WhenAnyValue(x => x.StartingHeight).Select(_ => !Validations.Any));
	}

	private void ValidateStartingHeight(IValidationErrors errors)
	{
		if (StartingHeight == "")
		{
			return;
		}

		if (!int.TryParse(StartingHeight, out _))
		{
			StartingHeight = new string(StartingHeight.Where(char.IsDigit).ToArray());
		}
	}
}
