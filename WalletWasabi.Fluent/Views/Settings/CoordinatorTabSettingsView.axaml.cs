using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.ViewModels.Settings;

namespace WalletWasabi.Fluent.Views.Settings;

public class CoordinatorTabSettingsView : UserControl
{
	public CoordinatorTabSettingsView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void OnKnownCoordinatorSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (sender is ComboBox combo
			&& e.AddedItems.Count > 0
			&& e.AddedItems[0] is KnownCoordinator item
			&& DataContext is CoordinatorTabSettingsViewModel vm)
		{
			vm.ApplyKnownCoordinator(item);
			combo.SelectedIndex = -1;
		}
	}
}
