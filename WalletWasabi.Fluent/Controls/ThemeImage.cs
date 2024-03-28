using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class ThemeImage: Image
{
	public ThemeImage()
	{
		ActualThemeVariantChanged += (s, e) => InvalidateVisual();	
	}
}
