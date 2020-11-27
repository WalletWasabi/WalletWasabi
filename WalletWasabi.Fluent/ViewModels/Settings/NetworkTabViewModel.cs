using System;
using System.Net;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class NetworkTabViewModel : SettingsViewModelBase
	{
		private bool _useTor;
		private bool _terminateTorOnExit;
		private string _torSocks5EndPoint;

		public NetworkTabViewModel(Global global, Config config) : base(global)
		{
			this.ValidateProperty(x => x.TorSocks5EndPoint, ValidateTorSocks5EndPoint);

			_useTor = config.UseTor;
			_terminateTorOnExit = config.TerminateTorOnExit;
			_torSocks5EndPoint = config.TorSocks5EndPoint.ToString(-1);

			this.WhenAnyValue(
					x => x.UseTor,
					x => x.UseTor,
					x => x.TorSocks5EndPoint)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(_ => Save());
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

		public string TorSocks5EndPoint
		{
			get => _torSocks5EndPoint;
			set => this.RaiseAndSetIfChanged(ref _torSocks5EndPoint, value);
		}

		private void ValidateTorSocks5EndPoint(IValidationErrors errors)
			=> ValidateEndPoint(errors, TorSocks5EndPoint, Constants.DefaultTorSocksPort, whiteSpaceOk: true);

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
			if (EndPointParser.TryParse(TorSocks5EndPoint, Constants.DefaultTorSocksPort, out EndPoint torEp))
			{
				config.TorSocks5EndPoint = torEp;
			}
			config.UseTor = UseTor;
			config.TerminateTorOnExit = TerminateTorOnExit;
		}
	}
}