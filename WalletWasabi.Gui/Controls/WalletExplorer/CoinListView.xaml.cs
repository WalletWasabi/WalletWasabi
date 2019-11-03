using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListView : UserControl
	{
		public static readonly StyledProperty<bool> SelectAllNonPrivateVisibleProperty =
			AvaloniaProperty.Register<CoinListView, bool>(nameof(SelectAllNonPrivateVisible), defaultBindingMode: BindingMode.TwoWay);

		public bool SelectAllNonPrivateVisible
		{
			get => GetValue(SelectAllNonPrivateVisibleProperty);
			set => SetValue(SelectAllNonPrivateVisibleProperty, value);
		}

		public CoinListView()
		{
			InitializeComponent();

			SelectAllNonPrivateVisible = true;

			this.WhenAnyValue(x => x.DataContext)
				.Subscribe(dataContext =>
				{
					if (dataContext is CoinListViewModel viewmodel)
					{
						viewmodel.SelectAllNonPrivateVisible = SelectAllNonPrivateVisible;
					}
				});
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
