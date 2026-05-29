using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Views.Shell;

public partial class TitleBar : UserControl
{
	public static readonly StyledProperty<Popup> SearchResultsPopupProperty = AvaloniaProperty.Register<TitleBar, Popup>(nameof(SearchResultsPopup));

	public TitleBar()
	{
		InitializeComponent();
	}
	
	/// <summary>
	/// Since Avalonia doesn't have a mechanism for binding to named descendants out of a given XAML file,
	/// we use the Visual Tree to find the Search Results Popup. We need to bind IsOpen to enable hit-test so the Title Bar
	/// is hittable while the Popup is shown. This way, the use can focus away, thus hiding the Results Popup.
	/// </summary>
	public Popup SearchResultsPopup
	{
		get => GetValue(SearchResultsPopupProperty);
		set => SetValue(SearchResultsPopupProperty, value);
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);

		SearchResultsPopup = this.GetVisualDescendants().OfType<Popup>().First(popup => popup.Name == "SearchResultsPopup");
	}

	private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
