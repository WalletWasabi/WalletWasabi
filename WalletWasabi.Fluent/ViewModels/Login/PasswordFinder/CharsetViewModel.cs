using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

public class CharsetViewModel : ViewModelBase
{
	public CharsetViewModel(SelectCharsetViewModel owner, Charset charset)
	{
		Title = charset.FriendlyName();
		ShortTitle = charset.ToString().ToUpper(CultureInfo.InvariantCulture);
		Characters = PasswordFinderHelper.Charsets.TryGetValue(charset, out var characters) ? characters : "";

		SelectCommand = new RelayCommand(() =>
		{
			owner.Options.Charset = charset;
			owner.Navigate().To(new ContainsNumbersViewModel(owner.Options));
		});
	}

	public string Title { get; }

	public string ShortTitle { get; }

	public string Characters { get; }

	public ICommand SelectCommand { get; }
}
