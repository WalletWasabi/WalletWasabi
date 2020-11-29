using System;
using System.Collections.Generic;
using System.Net;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class BitcoinTabTabViewModel : SettingsTabViewModelBase
	{
		private Network _network;
		private bool _startLocalBitcoinCoreOnStartup;
		private string _localBitcoinCoreDataDir;
		private bool _stopLocalBitcoinCoreOnShutdown;
		private string _bitcoinP2PEndPoint;

		public BitcoinTabTabViewModel(Config config, UiConfig uiConfig) : base(config, uiConfig)
		{
			this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);

			_network = config.Network;
			_startLocalBitcoinCoreOnStartup = config.StartLocalBitcoinCoreOnStartup;
			_localBitcoinCoreDataDir = config.LocalBitcoinCoreDataDir;
			_stopLocalBitcoinCoreOnShutdown = config.StopLocalBitcoinCoreOnShutdown;
			_bitcoinP2PEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);

			this.WhenAnyValue(
					x => x.Network,
					x => x.StartLocalBitcoinCoreOnStartup,
					x => x.StopLocalBitcoinCoreOnShutdown,
					x => x.BitcoinP2PEndPoint,
					x => x.LocalBitcoinCoreDataDir)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
				.Skip(1)
				.Subscribe(_ => Save());
		}

		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public Network Network
		{
			get => _network;
			set => this.RaiseAndSetIfChanged(ref _network, value);
		}

		public IEnumerable<Network> Networks => Network.GetNetworks();

		public bool StartLocalBitcoinCoreOnStartup
		{
			get => _startLocalBitcoinCoreOnStartup;
			set => this.RaiseAndSetIfChanged(ref _startLocalBitcoinCoreOnStartup, value);
		}

		public string LocalBitcoinCoreDataDir
		{
			get => _localBitcoinCoreDataDir;
			set => this.RaiseAndSetIfChanged(ref _localBitcoinCoreDataDir, value);
		}

		public bool StopLocalBitcoinCoreOnShutdown
		{
			get => _stopLocalBitcoinCoreOnShutdown;
			set => this.RaiseAndSetIfChanged(ref _stopLocalBitcoinCoreOnShutdown, value);
		}

		public string BitcoinP2PEndPoint
		{
			get => _bitcoinP2PEndPoint;
			set => this.RaiseAndSetIfChanged(ref _bitcoinP2PEndPoint, value);
		}

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

		protected override void EditConfigOnSave(Config config)
		{
			if (Network == config.Network)
			{
				if (EndPointParser.TryParse(BitcoinP2PEndPoint, Network.DefaultPort, out EndPoint p2PEp))
				{
					config.SetP2PEndpoint(p2PEp);
				}
				config.StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup;
				config.StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown;
				config.LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir);
			}
			else
			{
				config.Network = Network;
				BitcoinP2PEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
			}
		}
	}
}