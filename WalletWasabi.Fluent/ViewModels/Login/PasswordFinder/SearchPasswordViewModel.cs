using System;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public partial class SearchPasswordViewModel : RoutableViewModel
	{
		[AutoNotify] private int _percentage;
		[AutoNotify] private int _remainingHour;
		[AutoNotify] private int _remainingMin;
		[AutoNotify] private int _remainingSec;
		[AutoNotify] private string _hourText;
		[AutoNotify] private string _minText;
		[AutoNotify] private string _secText;


		public SearchPasswordViewModel(PasswordFinderOptions options)
		{
			Title = "Password Finder";

			Task.Run(() =>
			{
				PasswordFinderHelper.TryFind(options, out var foundPassword, SetStatus);
				Navigate().To(new PasswordFinderResultViewModel(foundPassword));
			});
		}

		private void SetStatus(int percentage, TimeSpan remainingTime)
		{
			Percentage = percentage;

			RemainingHour = remainingTime.Hours;
			RemainingMin = remainingTime.Minutes;
			RemainingSec = remainingTime.Seconds;

			HourText = $" hour{AddSIfPlural(RemainingHour)} ";
			MinText = $" minute{AddSIfPlural(RemainingMin)} ";
			SecText = $" second{AddSIfPlural(RemainingSec)}.";
		}

		private static string AddSIfPlural(int n) => n > 1 ? "s" : "";
	}
}
