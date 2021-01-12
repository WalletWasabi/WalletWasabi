using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveAddressViewModel : RoutableViewModel
	{
		[AutoNotify] private string _address;

		public ReceiveAddressViewModel(HdPubKey model)
		{
			_address = model.GetP2wpkhAddress(Network.TestNet).ToString();
		}
	}
}