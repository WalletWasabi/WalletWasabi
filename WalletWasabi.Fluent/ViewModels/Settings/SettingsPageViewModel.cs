using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private Network _network;
		private string _torSocks5EndPoint;
		private string _bitcoinP2PEndPoint;
		private string _localBitcoinCoreDataDir;
		private bool _useTor;
		private bool _startLocalBitcoinCoreOnStartup;
		private bool _stopLocalBitcoinCoreOnShutdown;
		private bool _isModified;
		private bool _terminateTorOnExit;
		private int _minimalPrivacyLevel;
		private int _mediumPrivacyLevel;
		private int _strongPrivacyLevel;
		private int _selectedTab;

		private Global Global;

		public SettingsPageViewModel(NavigationStateViewModel navigationState) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Title = "Settings";

			Global = Locator.Current.GetService<Global>();
			var config = new Config(Global.Config.FilePath);
			config.LoadOrCreateDefaultFile();

			GeneralTab = new GeneralTabViewModel(Global, config);

			this.ValidateProperty(x => x.TorSocks5EndPoint, ValidateTorSocks5EndPoint);
			this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);

			_selectedTab = 0;

			_network = config.Network;
			_torSocks5EndPoint = config.TorSocks5EndPoint.ToString(-1);
			UseTor = config.UseTor;
			TerminateTorOnExit = config.TerminateTorOnExit;
			StartLocalBitcoinCoreOnStartup = config.StartLocalBitcoinCoreOnStartup;
			StopLocalBitcoinCoreOnShutdown = config.StopLocalBitcoinCoreOnShutdown;

			_minimalPrivacyLevel = config.PrivacyLevelSome;
			_mediumPrivacyLevel = config.PrivacyLevelFine;
			_strongPrivacyLevel = config.PrivacyLevelStrong;


			_bitcoinP2PEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
			_localBitcoinCoreDataDir = config.LocalBitcoinCoreDataDir;

			IsModified = !Global.Config.AreDeepEqual(config);

			this.WhenAnyValue(
				x => x.Network,
				x => x.UseTor,
				x => x.TerminateTorOnExit,
				x => x.StartLocalBitcoinCoreOnStartup,
				x => x.StopLocalBitcoinCoreOnShutdown)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(_ => Save());

			OpenConfigFileCommand = ReactiveCommand.CreateFromTask(OpenConfigFileAsync);

			TextBoxLostFocusCommand = ReactiveCommand.Create(Save);

			Observable
				.Merge(OpenConfigFileCommand.ThrownExceptions)
				.Merge(TextBoxLostFocusCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));



			this.WhenAnyValue(x => x.MinimalPrivacyLevel)
				.Subscribe(
					x =>
					{
						if (x >= MediumPrivacyLevel)
						{
							MediumPrivacyLevel = x + 1;
						}
					});

			this.WhenAnyValue(x => x.MediumPrivacyLevel)
				.Subscribe(
					x =>
					{
						if (x >= StrongPrivacyLevel)
						{
							StrongPrivacyLevel = x + 1;
						}

						if (x <= MinimalPrivacyLevel)
						{
							MinimalPrivacyLevel = x - 1;
						}
					});

			this.WhenAnyValue(x => x.StrongPrivacyLevel)
				.Subscribe(
					x =>
					{
						if (x <= MinimalPrivacyLevel)
						{
							MinimalPrivacyLevel = x - 1;
						}

						if (x <= MediumPrivacyLevel)
						{
							MediumPrivacyLevel = x - 1;
						}
					});
		}

		public GeneralTabViewModel GeneralTab { get; }

		private object ConfigLock { get; } = new object();

		public ReactiveCommand<Unit, Unit> OpenConfigFileCommand { get; }

		public ReactiveCommand<Unit, Unit> TextBoxLostFocusCommand { get; }

		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public IEnumerable<Network> Networks => new[]
		{
			Network.Main,
			Network.TestNet,
			Network.RegTest
		};

		public Network Network
		{
			get => _network;
			set => this.RaiseAndSetIfChanged(ref _network, value);
		}

		public string TorSocks5EndPoint
		{
			get => _torSocks5EndPoint;
			set => this.RaiseAndSetIfChanged(ref _torSocks5EndPoint, value);
		}

		public string BitcoinP2PEndPoint
		{
			get => _bitcoinP2PEndPoint;
			set => this.RaiseAndSetIfChanged(ref _bitcoinP2PEndPoint, value);
		}

		public string LocalBitcoinCoreDataDir
		{
			get => _localBitcoinCoreDataDir;
			set => this.RaiseAndSetIfChanged(ref _localBitcoinCoreDataDir, value);
		}

		public bool IsModified
		{
			get => _isModified;
			set => this.RaiseAndSetIfChanged(ref _isModified, value);
		}

		public bool StartLocalBitcoinCoreOnStartup
		{
			get => _startLocalBitcoinCoreOnStartup;
			set => this.RaiseAndSetIfChanged(ref _startLocalBitcoinCoreOnStartup, value);
		}

		public bool StopLocalBitcoinCoreOnShutdown
		{
			get => _stopLocalBitcoinCoreOnShutdown;
			set => this.RaiseAndSetIfChanged(ref _stopLocalBitcoinCoreOnShutdown, value);
		}

		public bool UseTor
		{
			get => _useTor;
			set => this.RaiseAndSetIfChanged(ref _useTor, value);
		}

		public bool TerminateTorOnExit
		{
			get => _terminateTorOnExit;
			set => this.RaiseAndSetIfChanged(ref _terminateTorOnExit, value);
		}

		public int MinimalPrivacyLevel
		{
			get => _minimalPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _minimalPrivacyLevel, value);
		}

		public int MediumPrivacyLevel
		{
			get => _mediumPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _mediumPrivacyLevel, value);
		}

		public int StrongPrivacyLevel
		{
			get => _strongPrivacyLevel;
			set => this.RaiseAndSetIfChanged(ref _strongPrivacyLevel, value);
		}


		public int SelectedTab
		{
			get => _selectedTab;
			set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
		}

		public override string IconName => "settings_regular";

		private void Save()
		{
			var network = Network;

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
					if (Network == config.Network)
					{
						if (EndPointParser.TryParse(TorSocks5EndPoint, Constants.DefaultTorSocksPort, out EndPoint torEp))
						{
							config.TorSocks5EndPoint = torEp;
						}
						if (EndPointParser.TryParse(BitcoinP2PEndPoint, network.DefaultPort, out EndPoint p2PEp))
						{
							config.SetP2PEndpoint(p2PEp);
						}
						config.UseTor = UseTor;
						config.TerminateTorOnExit = TerminateTorOnExit;
						config.StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup;
						config.StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown;
						config.LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir);
						config.DustThreshold = decimal.TryParse(GeneralTab.DustThreshold, out var threshold) ? Money.Coins(threshold) : Config.DefaultDustThreshold;
						config.PrivacyLevelSome = MinimalPrivacyLevel;
						config.PrivacyLevelStrong = StrongPrivacyLevel;
						config.PrivacyLevelFine = MediumPrivacyLevel;
					}
					else
					{
						config.Network = Network;
						BitcoinP2PEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
					}
					config.ToFile();
					IsModified = !Global.Config.AreDeepEqual(config);
				}
			});
		}

		private async Task OpenConfigFileAsync()
		{
			await FileHelpers.OpenFileInTextEditorAsync(Global.Config.FilePath);
		}

		private void ValidateTorSocks5EndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: true);

		private void ValidateBitcoinP2PEndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, BitcoinP2PEndPoint, Network.DefaultPort, whiteSpaceOk: true);

		private void ValidateEndPoint(IValidationErrors errors, string endPoint, int defaultPort, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(endPoint))
			{
				if (!EndPointParser.TryParse(endPoint, defaultPort, out _))
				{
					errors.Add(ErrorSeverity.Error, "Invalid endpoint.");
				}
			}
		}
	}
}