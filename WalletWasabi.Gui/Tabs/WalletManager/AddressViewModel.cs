using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	public class AddressViewModel
	{
		private HdPubKey _model;

		public AddressViewModel(HdPubKey model)
		{
			_model = model;
			Address = _model.GetP2wpkhAddress(Global.Network).ToString();
		}

		public string Label => _model.Label;

		public string Address { get; }
	}
}
