using System.Globalization;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Advanced Recovery Options", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AdvancedRecoveryOptionsViewModel : DialogViewModelBase<(int MinGapLimit, uint BirthHeight)?>
{
	[AutoNotify] private string _minGapLimit;
	[AutoNotify] private string _birthHeight;

	private readonly uint _wasabiGenesisHeight;

	public AdvancedRecoveryOptionsViewModel(int minGapLimit, Height.ChainHeight wasabiGenesisHeight)
	{
		_minGapLimit = minGapLimit.ToString();
		_wasabiGenesisHeight = wasabiGenesisHeight;
		_birthHeight = wasabiGenesisHeight.Height.ToString(CultureInfo.InvariantCulture);

		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);
		this.ValidateProperty(x => x.BirthHeight, ValidateBirthHeight);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() => Close(result: (int.Parse(MinGapLimit), uint.Parse(BirthHeight))),
			this.WhenAnyValue(x => x.MinGapLimit, x => x.BirthHeight).Select(_ => !Validations.Any));
	}

	private void ValidateMinGapLimit(IValidationErrors errors)
	{
		if (!int.TryParse(MinGapLimit, out var minGapLimit) ||
			minGapLimit is < KeyManager.AbsoluteMinGapLimit or > KeyManager.MaxGapLimit)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number between {KeyManager.AbsoluteMinGapLimit} and {KeyManager.MaxGapLimit}.");
		}
	}

	private void ValidateBirthHeight(IValidationErrors errors)
	{
		if (!uint.TryParse(BirthHeight, out var height) || height < _wasabiGenesisHeight)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number greater than or equal to {_wasabiGenesisHeight}.");
		}
	}
}
