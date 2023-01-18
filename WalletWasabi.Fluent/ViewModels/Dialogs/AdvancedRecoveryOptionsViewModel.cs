using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
	[ObservableProperty] private string _minGapLimit;

	public AdvancedRecoveryOptionsViewModel(int minGapLimit)
	{
		this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		var backCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

		var nextCommandCanExecute = this.WhenAnyValue(
				x => x.IsDialogOpen,
				x => x.MinGapLimit,
				delegate
				{
					// This will fire validations before return canExecute value.
					OnPropertyChanged(nameof(MinGapLimit));

					return IsDialogOpen && !Validations.Any;
				})
			.ObserveOn(RxApp.MainThreadScheduler);

		var cancelCommandCanExecute = this.WhenAnyValue(x => x.IsDialogOpen).ObserveOn(RxApp.MainThreadScheduler);

		_minGapLimit = minGapLimit.ToString();

		BackCommand = ReactiveCommand.Create(() => Navigate().Back(), backCommandCanExecute);

		NextCommand = ReactiveCommand.Create(
			() => Close(result: int.Parse(MinGapLimit)),
			nextCommandCanExecute);

		CancelCommand = ReactiveCommand.Create(() => Close(), cancelCommandCanExecute);
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
