using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Views.Wallets.Home.History;

public class HistoryTable : UserControl
{
	public HistoryTable()
	{
		InitializeComponent();

		this.GetObservable(BoundsProperty)
			.Subscribe(bounds =>
			{
				if (bounds.Width > 0 && bounds.Width <= 600)
				{
					if (!Classes.Contains("mobile"))
					{
						Classes.Add("mobile");
					}
					// Also set on parent control if possible
					var parent = this.GetVisualParent();
					while (parent != null)
					{
						if (parent is UserControl uc)
						{
							if (!uc.Classes.Contains("mobile"))
							{
								uc.Classes.Add("mobile");
							}
						}
						parent = parent.GetVisualParent();
					}
				}
				else
				{
					Classes.Remove("mobile");
				}
			});
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
