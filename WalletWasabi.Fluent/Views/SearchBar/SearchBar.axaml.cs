using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar;

namespace WalletWasabi.Fluent.Views.SearchBar;

public class SearchBar : UserControl
{
	public SearchBar()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);

		var data = NavigationManager.MetaData
			.Select(m =>
			{
				var reactiveCommand = ReactiveCommand.Create(
					() => { });
				var searchItem = new SearchItem(m.Title, m.Caption, reactiveCommand){ Icon = m.IconName };
				return searchItem;
			});

		var items = data.ToObservable();

		DataContext = new SearchBarViewModel(items);
	}
}