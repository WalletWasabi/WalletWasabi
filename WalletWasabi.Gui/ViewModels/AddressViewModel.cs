using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.KeyManagement;
using Gma.QrCodeNet.Encoding;
using Gma.QrCodeNet.Encoding.Windows.Render;
using Avalonia.Media.Imaging;

namespace WalletWasabi.Gui.ViewModels
{
	public class AddressViewModel
	{
		public HdPubKey Model { get; }

		public AddressViewModel(HdPubKey model)
		{
			Model = model;
		}

		public string Label => Model.Label;

		public string Address => Model.GetP2wpkhAddress(Global.Network).ToString();


		public bool[,] QrCode
		{
			get
			{
				var encoder = new QrEncoder(ErrorCorrectionLevel.H);
				encoder.TryEncode(Address, out var qrCode);
				return qrCode.Matrix.InternalArray;
			}
		}
	}
}
