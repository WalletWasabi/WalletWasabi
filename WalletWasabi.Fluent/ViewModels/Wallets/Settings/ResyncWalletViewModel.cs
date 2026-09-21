using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(Title = "Resync Wallet", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ResyncWalletViewModel : DialogViewModelBase<(int StartingHeight, int MinGapLimit)?>
{
	[AutoNotify] private string _startingHeight = "";
	[AutoNotify] private string _minGapLimit;
	private readonly uint _birthHeight;
	private readonly int _currentMinGapLimit;

	public ResyncWalletViewModel(UiContext uiContext, uint birthHeight, int minGapLimit) : base(uiContext)
	{
		_birthHeight = birthHeight;
		_currentMinGapLimit = minGapLimit;
		_minGapLimit = minGapLimit.ToString();
		StartingHeight = birthHeight.ToString();
		this.ValidateProperty(x => x.StartingHeight, ValidateStartingHeight);
		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);

		SetupCancel(false, true, true);

		NextCommand = ReactiveCommand.Create(
			() =>
				Close(DialogResultKind.Normal, (StartingHeight is "" ? 0 : int.Parse(StartingHeight), int.Parse(MinGapLimit))),
			this.WhenAnyValue(x => x.StartingHeight, x => x.MinGapLimit).Select(_ => !Validations.Any));
	}

	private void ValidateMinGapLimit(IValidationErrors errors)
	{
		if (!int.TryParse(MinGapLimit, out var minGapLimit) || minGapLimit < _currentMinGapLimit || minGapLimit > KeyManager.MaxGapLimit)
		{
			errors.Add(ErrorSeverity.Error, $"Must be a number between {_currentMinGapLimit} and {KeyManager.MaxGapLimit}.");
		}
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
