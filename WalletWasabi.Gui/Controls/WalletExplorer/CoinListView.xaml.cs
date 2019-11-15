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

		public bool SelectAllNonPrivateVisible
		{
			get => GetValue(SelectAllNonPrivateVisibleProperty);
			set => SetValue(SelectAllNonPrivateVisibleProperty, value);
		}

		public static readonly StyledProperty<bool> SelectAllPrivateVisibleProperty =
			AvaloniaProperty.Register<CoinListView, bool>(nameof(SelectAllPrivateVisible), defaultBindingMode: BindingMode.OneWayToSource);

		public bool SelectAllPrivateVisible
		{
			get => GetValue(SelectAllPrivateVisibleProperty);
			set => SetValue(SelectAllPrivateVisibleProperty, value);
		}

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

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
