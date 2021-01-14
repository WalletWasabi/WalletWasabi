using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Gma.QrCodeNet.Encoding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveAddressViewModel : RoutableViewModel
	{
		[AutoNotify] private bool _animationTrigger;
		[AutoNotify] private string _actionText;

		public ReceiveAddressViewModel(HdPubKey model, Network network, HDFingerprint? masterFingerprint, bool isHardwareWallet)
		{
			Title = "Receive";
			Address = model.GetP2wpkhAddress(network).ToString();
			Reference = model.Label;
			IsHardwareWallet = isHardwareWallet;
			_actionText = "";

			GenerateQrCode();

			CopyAddressCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				ActionText = "Copied";
				await Application.Current.Clipboard.SetTextAsync(Address);
			});

			ShowOnHwWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (masterFingerprint is null)
				{
					return;
				}

				ActionText = "Sending";

				await Task.Run(async () =>
				{
					try
					{
						var client = new HwiClient(network);
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

						await client.DisplayAddressAsync(masterFingerprint.Value, model.FullKeyPath, cts.Token);
					}
					catch (FormatException ex) when (ex.Message.Contains("network") && network == Network.TestNet)
					{
						// This exception happens everytime on TestNet because of Wasabi Keypath handling.
						// The user don't need know about it.
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
						await ShowErrorAsync(ex.ToUserFriendlyString(), "We were unable to send the address to the device"); // TODO: FIX NAVIGATION BACK
					}
				});
			});

			ObserveToAnimationTrigger(CopyAddressCommand, ShowOnHwWalletCommand);

			NextCommand = CancelCommand;
		}

		public ReactiveCommand<Unit, Unit> CopyAddressCommand { get; set; }

		public ICommand ShowOnHwWalletCommand { get; set; }

		public string Address { get; }

		public string Reference { get; }

		public bool[,]? QrCode { get; set; }

		public bool IsHardwareWallet { get; }

		private void ObserveToAnimationTrigger(params ICommand[] commands)
		{
			foreach (var cmd in commands)
			{
				(cmd as IReactiveCommand)
					?.IsExecuting
					.ObserveOn(RxApp.MainThreadScheduler)
					.Skip(1)
					.Subscribe(x => AnimationTrigger = x);
			}
		}

		private void GenerateQrCode()
		{
			try
			{
				var encoder = new QrEncoder();
				encoder.TryEncode(Address, out var qrCode);
				QrCode = qrCode.Matrix.InternalArray;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}