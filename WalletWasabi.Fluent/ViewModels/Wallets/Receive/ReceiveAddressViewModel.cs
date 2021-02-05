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

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive
{
	[NavigationMetaData(Title = "Receive Address")]
	public partial class ReceiveAddressViewModel : RoutableViewModel
	{
		public ReceiveAddressViewModel(HdPubKey model, Network network, HDFingerprint? masterFingerprint, bool isHardwareWallet)
		{
			Address = model.GetP2wpkhAddress(network).ToString();
			Reference = model.Label;
			IsHardwareWallet = isHardwareWallet;

			GenerateQrCode();

			CopyAddressCommand = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Address));

			ShowOnHwWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (masterFingerprint is null)
				{
					return;
				}

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
						// The user doesn't need to know about it.
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
						await ShowErrorAsync(ex.ToUserFriendlyString(), "We were unable to send the address to the device");
					}
				});
			});

			SaveQrCodeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (QrCodeCommand is { } cmd)
				{
					await cmd.Execute(Address);
				}
			});

			NextCommand = CancelCommand;
		}

		public ReactiveCommand<string, Unit>? QrCodeCommand { get; set; }

		public ICommand CopyAddressCommand { get; }

		public ICommand SaveQrCodeCommand { get; }

		public ICommand ShowOnHwWalletCommand { get; }

		public string Address { get; }

		public string Reference { get; }

		public bool[,]? QrCode { get; set; }

		public bool IsHardwareWallet { get; }

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
