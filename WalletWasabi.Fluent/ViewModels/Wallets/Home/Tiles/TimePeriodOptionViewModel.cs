using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class TimePeriodOptionViewModel
{
	[AutoNotify] private bool _isSelected;

	public TimePeriodOptionViewModel(TimePeriodOption option, Action<TimePeriodOptionViewModel> updateAction)
	{
		Option = option;
		Text = option.FriendlyName();
		SelectCommand = ReactiveCommand.Create(() => updateAction(this));
	}

	public TimePeriodOption Option { get; }

	public string Text { get; }

	public ICommand SelectCommand { get; }
}
