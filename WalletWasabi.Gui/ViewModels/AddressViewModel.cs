using Avalonia;
using Gma.QrCodeNet.Encoding;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.ViewModels
{
	public class AddressViewModel : ViewModelBase
	{
		private bool _isExpanded;
		private bool _generating;
		private bool _isBusy;
		private bool[,] _qrCode;

		public HdPubKey Model { get; }

		public AddressViewModel(HdPubKey model)
		{
			Model = model;

			this.WhenAnyValue(x => x.IsExpanded).Subscribe(async expanded =>
			{
				if (expanded && !_generating && QrCode == null)
				{
					IsBusy = true;

					_generating = true;

					QrCode = await Task.Run(() =>
					{
						var encoder = new QrEncoder(ErrorCorrectionLevel.H);
						encoder.TryEncode(Address, out var qrCode);

						return qrCode.Matrix.InternalArray;
					});

					IsBusy = false;
				}
			});
		}

		public bool IsBusy
		{
			get { return _isBusy; }
			set { this.RaiseAndSetIfChanged(ref _isBusy, value); }
		}

		public bool IsExpanded
		{
			get { return _isExpanded; }
			set { this.RaiseAndSetIfChanged(ref _isExpanded, value); }
		}

		public string Label => Model.Label;

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();

		public bool[,] QrCode
		{
			get => _qrCode;
			set => this.RaiseAndSetIfChanged(ref _qrCode, value);
		}

		public void CopyToClipboard()
		{
			Application.Current.Clipboard.SetTextAsync(Address).GetAwaiter().GetResult();
		}
	}
}
