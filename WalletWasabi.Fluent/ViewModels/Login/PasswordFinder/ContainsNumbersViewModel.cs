using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public class ContainsNumbersViewModel : RoutableViewModel
	{
		public ContainsNumbersViewModel(PasswordFinderOptions options)
		{
			Title = "Password Finder";

			AnswerCommand = ReactiveCommand.Create<bool>(ans =>
			{
				options.UseNumbers = ans;
				Navigate().To(new ContainsSymbolsViewModel(options));
			});
		}

		public ReactiveCommand<bool, Unit> AnswerCommand { get; }
	}
}