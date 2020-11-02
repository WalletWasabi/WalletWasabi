using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Fluent.AddWallet.Common;
using WalletWasabi.Gui;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AddWalletPageViewModel : NavBarItemViewModel
	{
		private string _walletName = "";
		private bool _optionsEnabled;

		public AddWalletPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Add Wallet";

			this.WhenAnyValue(x => x.WalletName)
				.Select(x => !string.IsNullOrWhiteSpace(x))
				.Subscribe(x => OptionsEnabled = x);

			CreateWalletCommand = ReactiveCommand.Create(() =>
			{
				var global = Locator.Current.GetService<Global>();

				screen.Router.Navigate.Execute(new EnterPasswordViewModel(screen, global, WalletName));
			});
		}

		public override string IconName => "add_circle_regular";

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

		public ICommand CreateWalletCommand { get; }
	}
}