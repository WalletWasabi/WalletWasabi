using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.KeyManagement;

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
	}
}
