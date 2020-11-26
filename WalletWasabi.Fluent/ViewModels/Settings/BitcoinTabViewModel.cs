using System;
using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class BitcoinTabViewModel : ViewModelBase
	{
		private Network _network;
		private bool _startLocalBitcoinCoreOnStartup;
		private string _localBitcoinCoreDataDir;
		private bool _stopLocalBitcoinCoreOnShutdown;
		private string _bitcoinP2PEndPoint;

		public BitcoinTabViewModel(Config config)
		{
			this.ValidateProperty(x => x.BitcoinP2PEndPoint, ValidateBitcoinP2PEndPoint);

			_network = config.Network;
			StartLocalBitcoinCoreOnStartup = config.StartLocalBitcoinCoreOnStartup;
			_localBitcoinCoreDataDir = config.LocalBitcoinCoreDataDir;
			StopLocalBitcoinCoreOnShutdown = config.StopLocalBitcoinCoreOnShutdown;
			_bitcoinP2PEndPoint = config.GetP2PEndpoint().ToString(defaultPort: -1);
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
	}
}