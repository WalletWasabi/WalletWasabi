namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public partial class PocketViewModel : ViewModelBase
	{
		[AutoNotify] private bool _isSelected;
		[AutoNotify] private decimal _totalBtc;
		[AutoNotify] private string _labels;
	}
}