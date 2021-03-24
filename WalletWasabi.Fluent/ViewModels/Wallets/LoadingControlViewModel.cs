namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class LoadingControlViewModel : ViewModelBase
	{
		[AutoNotify] private double _percent;
		[AutoNotify] private string? _statusText;

		public LoadingControlViewModel()
		{
			_statusText = "";
			_percent = 0;
		}
	}
}