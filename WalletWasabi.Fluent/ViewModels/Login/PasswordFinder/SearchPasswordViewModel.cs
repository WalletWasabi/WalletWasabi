using System;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public partial class SearchPasswordViewModel : RoutableViewModel
	{
		[AutoNotify] private int _percentage;
		[AutoNotify] private string _remainingText;

		public SearchPasswordViewModel(PasswordFinderOptions options)
		{
			Title = "Password Finder";
			_remainingText = "";

			Task.Run(() =>
			{
				var passwordFound = WalletWasabi.Wallets.PasswordFinder.PasswordFinder.TryFind(
					options,
					out var foundPassword,
					SetStatus);

				Navigate().To(new PasswordFinderResultViewModel(foundPassword));
			});
		}

		private void SetStatus(int percentage, TimeSpan remainingTime)
		{
			Percentage = percentage;

			var h = remainingTime.Hours;
			var m = remainingTime.Minutes;
			var s = remainingTime.Seconds;

			RemainingText = "The search will finish in " +
							$"{h} hour{AddSIfPlural(h)}, " +
							$"{m} minute{AddSIfPlural(m)}, " +
							$"{s} second{AddSIfPlural(s)}.";
		}

		private string AddSIfPlural(int n) => n > 1 ? "s" : "";
	}
}
