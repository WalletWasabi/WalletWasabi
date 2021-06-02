using System;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Model;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class TimePeriodOptionViewModel
	{
		public TimePeriodOption Option { get; }
		[AutoNotify] private bool _isSelected;

		public TimePeriodOptionViewModel(TimePeriodOption option, Action<TimePeriodOptionViewModel> updateAction, uint orderIndex)
		{
			Option = option;
			Text = option.FriendlyName();
			SelectCommand = ReactiveCommand.Create(() => updateAction(this));
			OrderIndex = orderIndex;
		}

		public string Text { get; }

		public uint OrderIndex { get; }

		public ICommand SelectCommand { get; }
	}
}
