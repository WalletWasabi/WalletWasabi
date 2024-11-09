using System.Globalization;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "AdvancedRecoveryOptionsViewModel_Title", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AdvancedRecoveryOptionsViewModel : DialogViewModelBase<int?>
{
	[AutoNotify] private string _minGapLimit;

	public AdvancedRecoveryOptionsViewModel(int minGapLimit)
	{
		_minGapLimit = minGapLimit.ToString(CultureInfo.InvariantCulture);

		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() => Close(result: int.Parse(MinGapLimit, CultureInfo.InvariantCulture)),
			this.WhenAnyValue(x => x.MinGapLimit).Select(_ => !Validations.Any));
	}

	private void ValidateMinGapLimit(IValidationErrors errors)
	{
		if (!int.TryParse(MinGapLimit, out var minGapLimit) ||
			minGapLimit is < KeyManager.AbsoluteMinGapLimit or > KeyManager.MaxGapLimit)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"{Lang.Resources.Sentences_MustBeANumberBetween} {KeyManager.AbsoluteMinGapLimit} {Lang.Utils.LowerCaseFirst(Lang.Resources.Words_And)} {KeyManager.MaxGapLimit}.");
		}
	}
}
