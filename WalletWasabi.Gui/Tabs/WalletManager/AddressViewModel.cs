namespace WalletWasabi.Gui.Tabs.WalletManager
{
	public class AddressViewModel
	{
		public AddressViewModel(string label, string address)
		{
			Label = label;
			Address = address;
		}

		public string Label { get; }

		public string Address { get; }
	}
}
