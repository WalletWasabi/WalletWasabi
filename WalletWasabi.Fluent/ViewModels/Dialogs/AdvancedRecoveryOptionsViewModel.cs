using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Advanced Recovery Options")]
public partial class AdvancedRecoveryOptionsViewModel : DialogViewModelBase<int?>
{
	[ObservableProperty] [NotifyCanExecuteChangedFor(nameof(NextCommand))] private string _minGapLimit;

	public AdvancedRecoveryOptionsViewModel(int minGapLimit)
	{
		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_minGapLimit = minGapLimit.ToString();

		BackCommand = new RelayCommand(() => Navigate().Back());

		NextCommand = new RelayCommand(
			() => Close(result: int.Parse(MinGapLimit)),
			() => !Validations.Any);

		CancelCommand = new RelayCommand(() => Close());
	}

	private void ValidateMinGapLimit(IValidationErrors errors)
	{
		if (!int.TryParse(MinGapLimit, out var minGapLimit) || minGapLimit < KeyManager.AbsoluteMinGapLimit ||
			minGapLimit > KeyManager.MaxGapLimit)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number between {KeyManager.AbsoluteMinGapLimit} and {KeyManager.MaxGapLimit}.");
		}
	}

	protected override void OnDialogClosed()
	{
	}
}
