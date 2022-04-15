using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class DarkThemeSelector : ReactiveObject
{
	private bool _isDarkThemeEnabled;

	public DarkThemeSelector()
	{
		IsDarkThemeEnabled = ThemeHelper.CurrentTheme == Theme.Dark;
		this.WhenAnyValue(x => x.IsDarkThemeEnabled)
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

	public bool IsDarkThemeEnabled
	{
		get => _isDarkThemeEnabled;
		set => this.RaiseAndSetIfChanged(ref _isDarkThemeEnabled, value);
	}
}