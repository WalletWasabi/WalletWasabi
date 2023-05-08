using System.Globalization;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

public partial class CharsetViewModel : ViewModelBase
{
	private CharsetViewModel(IPasswordFinderModel model, Charset charset)
	{
		Title = charset.FriendlyName();
		ShortTitle = charset.ToString().ToUpper(CultureInfo.InvariantCulture);
		Characters = PasswordFinderHelper.Charsets.TryGetValue(charset, out var characters) ? characters : "";

		SelectCommand = ReactiveCommand.Create(() =>
		{
			model.Charset = charset;
			UiContext.Navigate().To().ContainsNumbers(model);
		});
	}

	public string Title { get; }

	public string ShortTitle { get; }

	public string Characters { get; }

	public ICommand SelectCommand { get; }
}
