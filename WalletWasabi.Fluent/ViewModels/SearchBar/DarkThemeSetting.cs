using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class DarkThemeSetting : Setting<UiConfig, bool>
{
	public DarkThemeSetting(UiConfig uiConfig) : base(uiConfig, config => config.DarkModeEnabled)
	{
		this.WhenAnyValue(x => x.Value)
			.SelectMany(b =>
			{
				return Observable.FromAsync(() =>
				{
					ThemeHelper.ApplyTheme(b ? Theme.Dark : Theme.Light);
					return Task.FromResult(Unit.Default);
				});
			})
			.Subscribe();
	}
}