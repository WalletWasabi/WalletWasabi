using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinDetailsGrid : UserControl
	{
		public static readonly StyledProperty<CoinViewModel> SelectedCoinProperty =
			AvaloniaProperty.Register<CoinListView, CoinViewModel>(nameof(SelectedCoin));

		public CoinViewModel SelectedCoin
		{
			get => GetValue(SelectedCoinProperty);
			set => SetValue(SelectedCoinProperty, value);
		}

		public static readonly StyledProperty<bool> IsShownProperty =
			AvaloniaProperty.Register<CoinListView, bool>(nameof(IsShown));

		public bool IsShown
		{
			get => GetValue(IsShownProperty);
			set => SetValue(IsShownProperty, value);
		}

		public CoinDetailsGrid()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
