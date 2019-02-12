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
		private bool[,] _qrCode;

		public HdPubKey Model { get; }

		public AddressViewModel(HdPubKey model)
		{
			Model = model;

			Task.Run(() =>
			{
				var encoder = new QrEncoder(ErrorCorrectionLevel.M);
				encoder.TryEncode(Address, out var qrCode);

				return qrCode.Matrix.InternalArray;
			}).ContinueWith(x=> {
				QrCode = x.Result;
			});
		}

		public bool IsExpanded
		{
			get { return _isExpanded; }
			set { this.RaiseAndSetIfChanged(ref _isExpanded, value); }
		}

		public string Label => Model.Label;

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();

		public string Pubkey => Model.PubKey.ToString();

		public string KeyPath => Model.FullKeyPath.ToString();

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
