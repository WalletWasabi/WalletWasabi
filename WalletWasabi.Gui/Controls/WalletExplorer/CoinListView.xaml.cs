using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinListView : UserControl
	{
		public static readonly StyledProperty<bool> SelectAllNonPrivateVisibleProperty =
			AvaloniaProperty.Register<CoinListView, bool>(nameof(SelectAllNonPrivateVisible), defaultBindingMode: BindingMode.OneWayToSource);

		public static readonly StyledProperty<bool> SelectAllPrivateVisibleProperty =
			AvaloniaProperty.Register<CoinListView, bool>(nameof(SelectAllPrivateVisible), defaultBindingMode: BindingMode.OneWayToSource);

		public CoinListView()
		{
			InitializeComponent();

			SelectAllNonPrivateVisible = true;
			SelectAllPrivateVisible = true;

			this.WhenAnyValue(x => x.DataContext)
				.Subscribe(dataContext =>
				{
					if (dataContext is CoinListViewModel viewmodel)
					{
						// Value is only propagated when DataContext is set at the beginning.
						viewmodel.SelectAllNonPrivateVisible = SelectAllNonPrivateVisible;
						viewmodel.SelectAllPrivateVisible = SelectAllPrivateVisible;
					}
				});
		}

		public bool SelectAllNonPrivateVisible
		{
			get => GetValue(SelectAllNonPrivateVisibleProperty);
			set => SetValue(SelectAllNonPrivateVisibleProperty, value);
		}

		public bool SelectAllPrivateVisible
		{
			get => GetValue(SelectAllPrivateVisibleProperty);
			set => SetValue(SelectAllPrivateVisibleProperty, value);
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
