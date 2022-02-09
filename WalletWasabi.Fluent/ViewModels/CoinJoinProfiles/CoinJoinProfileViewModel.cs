using Avalonia.Media;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

public class CoinJoinProfileViewModel : ViewModelBase
{
	public string Title { get; }
	public string Description { get; }
	public IImage Icon { get; }

	public CoinJoinProfileViewModel(string title, string description)
	{
		Title = title;
		Description = description;
		Icon = AssetHelpers.GetBitmapAsset($"avares://WalletWasabi.Fluent/Assets/PasswordFinder/{ThemeHelper.CurrentTheme}/numbers.png");
	}
}
