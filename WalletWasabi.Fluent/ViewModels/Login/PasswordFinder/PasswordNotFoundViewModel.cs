using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public class PasswordNotFoundViewModel : RoutableViewModel
	{
		public PasswordNotFoundViewModel()
		{

		}

		public ICommand YesCommand { get; }

		public ICommand NoCommand { get; }

	}
}