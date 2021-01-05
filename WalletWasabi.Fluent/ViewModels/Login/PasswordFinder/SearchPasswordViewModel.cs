using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public partial class SearchPasswordViewModel : RoutableViewModel
	{
		[AutoNotify] private int _percentage;
		[AutoNotify] private string _remainingText;

		public SearchPasswordViewModel(PasswordFinderOptions options)
		{
			Title = "Password Finder";
			_remainingText = "adsadsadasdasdasd";
		}
	}
}