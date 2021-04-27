using System;
using System.Collections.Generic;
using System.Net;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	[NavigationMetaData(
		Title = "Bitcoin",
		Caption = "Manage Bitcoin settings",
		Order = 3,
		Category = "Settings",
		Keywords = new[]
		{
			"Settings", "Bitcoin", "Network", "Main", "TestNet", "RegTest", "Run", "Knots", "Startup", "P2P", "Endpoint"
		},
		IconName = "settings_bitcoin_regular")]
	public partial class BitcoinTabSettingsViewModel : SettingsTabViewModelBase
	{
		[AutoNotify] private Network _network;
		[AutoNotify] private bool _startLocalBitcoinCoreOnStartup;
		[AutoNotify] private string _localBitcoinCoreDataDir;
		[AutoNotify] private bool _stopLocalBitcoinCoreOnShutdown;
		[AutoNotify] private string _bitcoinP2PEndPoint;
		[AutoNotify] private string _dustThreshold;

		public BitcoinTabSettingsViewModel(Config config) : base(config)
		{
			this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);
			this.ValidateProperty(x => x.DustThreshold, ValidateDustThreshold);

			_network = config.Network;
			_startLocalBitcoinCoreOnStartup = config.StartLocalBitcoinCoreOnStartup;
			_localBitcoinCoreDataDir = config.LocalBitcoinCoreDataDir;
			_stopLocalBitcoinCoreOnShutdown = config.StopLocalBitcoinCoreOnShutdown;
			_bitcoinP2PEndPoint = config.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
			_dustThreshold = config.DustThreshold.ToString();

			this.WhenAnyValue(
					x => x.Network,
					x => x.StartLocalBitcoinCoreOnStartup,
					x => x.StopLocalBitcoinCoreOnShutdown,
					x => x.BitcoinP2PEndPoint,
					x => x.LocalBitcoinCoreDataDir,
					x => x.DustThreshold)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
				.Skip(1)
				.Subscribe(_ => Save());
		}

		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;

		public IEnumerable<Network> Networks => Network.GetNetworks();

		private void ValidateBitcoinP2PEndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, BitcoinP2PEndPoint, Network.DefaultPort, whiteSpaceOk: true);

		private static void ValidateEndPoint(IValidationErrors errors, string endPoint, int defaultPort, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(endPoint))
			{
				if (!EndPointParser.TryParse(endPoint, defaultPort, out _))
				{
					errors.Add(ErrorSeverity.Error, "Invalid endpoint.");
				}
			}
		}

		private void ValidateDustThreshold(IValidationErrors errors) =>
			ValidateDustThreshold(errors, DustThreshold, whiteSpaceOk: true);

		private static void ValidateDustThreshold(IValidationErrors errors, string dustThreshold, bool whiteSpaceOk)
		{
			if (!whiteSpaceOk || !string.IsNullOrWhiteSpace(dustThreshold))
			{
				if (!string.IsNullOrEmpty(dustThreshold) && dustThreshold.Contains(
					',',
					StringComparison.InvariantCultureIgnoreCase))
				{
					errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
				}

				if (!decimal.TryParse(dustThreshold, out var dust) || dust < 0)
				{
					errors.Add(ErrorSeverity.Error, "Invalid dust threshold.");
				}
			}
		}

		protected override void EditConfigOnSave(Config config)
		{
			if (Network == config.Network)
			{
				if (EndPointParser.TryParse(BitcoinP2PEndPoint, Network.DefaultPort, out EndPoint? p2PEp))
				{
					config.SetBitcoinP2pEndpoint(p2PEp);
				}

				config.StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup;
				config.StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown;
				config.LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir);
				config.DustThreshold = decimal.TryParse(DustThreshold, out var threshold)
					? Money.Coins(threshold)
					: Config.DefaultDustThreshold;
			}
			else
			{
				config.Network = Network;
				BitcoinP2PEndPoint = config.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
			}
		}
	}
}
