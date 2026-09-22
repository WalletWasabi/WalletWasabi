using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

public record ResyncWalletDialogResult(uint StartingHeight, int MinGapLimit);

[NavigationMetaData(Title = "Resync Wallet", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ResyncWalletViewModel : DialogViewModelBase<ResyncWalletDialogResult?>
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
			{
				var result = new ResyncWalletDialogResult(StartingHeight is "" ? 0u : uint.Parse(StartingHeight), int.Parse(MinGapLimit));
				Close(DialogResultKind.Normal, result);
			},
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
