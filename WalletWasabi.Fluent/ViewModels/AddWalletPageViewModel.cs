using System.Reactive.Linq;
using ReactiveUI;
using System;
using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

		public AddWalletPageViewModel(NavigationStateViewModel navigationState) : base(navigationState, NavigationTarget.Dialog)
		{
			Title = "Add Wallet";

			BackCommand = ReactiveCommand.Create(() => navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear());
			CancelCommand = ReactiveCommand.Create(() => navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear());

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x);
		}

		public override string IconName => "add_circle_regular";

		public ICommand BackCommand { get; }
		public ICommand CancelCommand { get; }

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}

		public bool OptionsEnabled
		{
			get => _optionsEnabled;
			set => this.RaiseAndSetIfChanged(ref _optionsEnabled, value);
		}
	}
}
