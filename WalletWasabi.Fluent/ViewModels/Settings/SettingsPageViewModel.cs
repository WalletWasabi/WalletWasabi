using System;
using System.Collections.Generic;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private bool _isModified;
		private int _selectedTab;

		public SettingsPageViewModel(NavigationStateViewModel navigationState, Global global) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Global = global;
			Title = "Settings";

			var config = new Config(global.Config.FilePath);
			config.LoadOrCreateDefaultFile();

			GeneralTab = new GeneralTabViewModel(Global, config);
			PrivacyTab = new PrivacyTabViewModel(config);
			NetworkTab = new NetworkTabViewModel(config);
			BitcoinTab = new BitcoinTabViewModel(config);

			_selectedTab = 0;

			// TODO: Restart wasabi message
			IsModified = !Global.Config.AreDeepEqual(config);

			// TODO: trigger save
			// this.WhenAnyValue(
			// 	x => x.Network,
			// 	x => x.UseTor,
			// 	x => x.TerminateTorOnExit,
			// 	x => x.StartLocalBitcoinCoreOnStartup,
			// 	x => x.StopLocalBitcoinCoreOnShutdown)
			// 	.ObserveOn(RxApp.TaskpoolScheduler)
			// 	.Subscribe(_ => Save());

			TextBoxLostFocusCommand = ReactiveCommand.Create(Save);

			Observable
				.Merge(TextBoxLostFocusCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		public Global Global { get; }

		public GeneralTabViewModel GeneralTab { get; }
		public PrivacyTabViewModel PrivacyTab { get; }
		public NetworkTabViewModel NetworkTab { get; }
		public BitcoinTabViewModel BitcoinTab { get; }

		private object ConfigLock { get; } = new object();

		public ReactiveCommand<Unit, Unit> TextBoxLostFocusCommand { get; }

		public bool IsModified
		{
			get => _isModified;
			set => this.RaiseAndSetIfChanged(ref _isModified, value);
		}

		public int SelectedTab
		{
			get => _selectedTab;
			set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
		}

		public override string IconName => "settings_regular";

		private void Save()
		{
			var network = BitcoinTab.Network;

			if (Validations.Any)
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(
				() =>
			{
				lock (ConfigLock)
				{
					config.LoadFile();
					if (BitcoinTab.Network == config.Network)
					{
						if (EndPointParser.TryParse(NetworkTab.TorSocks5EndPoint, Constants.DefaultTorSocksPort, out EndPoint torEp))
						{
							config.TorSocks5EndPoint = torEp;
						}
						if (EndPointParser.TryParse(BitcoinTab.BitcoinP2PEndPoint, network.DefaultPort, out EndPoint p2PEp))
						{
							config.SetP2PEndpoint(p2PEp);
						}
						config.UseTor = NetworkTab.UseTor;
						config.TerminateTorOnExit = NetworkTab.TerminateTorOnExit;
						config.StartLocalBitcoinCoreOnStartup = BitcoinTab.StartLocalBitcoinCoreOnStartup;
						config.StopLocalBitcoinCoreOnShutdown = BitcoinTab.StopLocalBitcoinCoreOnShutdown;
						config.LocalBitcoinCoreDataDir = Guard.Correct(BitcoinTab.LocalBitcoinCoreDataDir);
						config.DustThreshold = decimal.TryParse(GeneralTab.DustThreshold, out var threshold) ? Money.Coins(threshold) : Config.DefaultDustThreshold;
						config.PrivacyLevelSome = PrivacyTab.MinimalPrivacyLevel;
						config.PrivacyLevelStrong = PrivacyTab.StrongPrivacyLevel;
						config.PrivacyLevelFine = PrivacyTab.MediumPrivacyLevel;
					}
					else
					{
						config.Network = BitcoinTab.Network;
						BitcoinTab.BitcoinP2PEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
					}
					config.ToFile();
					IsModified = !Global.Config.AreDeepEqual(config);
				}
			});
		}
	}
}