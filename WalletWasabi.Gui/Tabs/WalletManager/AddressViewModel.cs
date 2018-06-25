using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.WalletManager
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
