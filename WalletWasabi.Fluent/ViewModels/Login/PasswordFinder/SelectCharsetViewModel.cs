using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class SelectCharsetViewModel : RoutableViewModel
{
	private SelectCharsetViewModel(IPasswordFinderModel model)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		Charsets = Enum.GetValues(typeof(Charset)).Cast<Charset>().Select(x => new CharsetViewModel(UiContext, model, x));
	}

	public IEnumerable<CharsetViewModel> Charsets { get; }
}
