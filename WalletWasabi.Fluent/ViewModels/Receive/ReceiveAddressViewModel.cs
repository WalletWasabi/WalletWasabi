using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Gma.QrCodeNet.Encoding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveAddressViewModel : RoutableViewModel
	{
		[AutoNotify] private bool _copyAnimationTrigger;

		public ReceiveAddressViewModel(HdPubKey model, Network network)
		{
			Title = "Receive";
			Address = model.GetP2wpkhAddress(network).ToString();
			Reference = model.Label;

			GenerateQrCode();

			CopyAddressCommand = ReactiveCommand.CreateFromTask(async () => await Application.Current.Clipboard.SetTextAsync(Address));

			CopyAddressCommand
				.IsExecuting
				.ObserveOn(RxApp.MainThreadScheduler)
				.Skip(1)
				.Subscribe(x => CopyAnimationTrigger = x);

			NextCommand = CancelCommand;
		}

		public ReactiveCommand<Unit, Unit> CopyAddressCommand { get; set; }

		public string Address { get; }

		public string Reference { get; }

		public bool[,]? QrCode { get; set; }

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