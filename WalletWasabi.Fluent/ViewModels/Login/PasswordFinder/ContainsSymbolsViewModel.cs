using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public class ContainsSymbolsViewModel : RoutableViewModel
	{
		public ContainsSymbolsViewModel(PasswordFinderOptions options)
		{
			Title = "Password Finder";

			AnswerCommand = ReactiveCommand.Create<bool>(ans =>
			{
				options.UseSymbols = ans;
				Navigate().To(new SearchPasswordViewModel(options));
			});
		}

		public ReactiveCommand<bool, Unit> AnswerCommand { get; set; }
	}
}