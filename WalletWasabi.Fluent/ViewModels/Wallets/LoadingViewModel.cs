namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class LoadingViewModel : ViewModelBase
	{
		[AutoNotify] private double _percent;
		[AutoNotify] private string? _statusText;

		public LoadingViewModel()
		{
			_statusText = "";
			_percent = 0;
		}
	}
}
