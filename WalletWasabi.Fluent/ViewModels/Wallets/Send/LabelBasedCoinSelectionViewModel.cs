using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Converters;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelBasedCoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<CoinGroup> _items;
	[AutoNotify] private string _filter = "";

	public LabelBasedCoinSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges)
	{
		var filterPredicate = this
			.WhenAnyValue(x => x.Filter)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
			.DistinctUntilChanged()
			.Select(SearchItemFilterFunc);

		coinChanges
			.Group(x => x.SmartLabel)
			.TransformWithInlineUpdate(group => new CoinGroup(group.Key, group.Cache))
			.Filter(filterPredicate)
			//.DisposeMany()	// Disposal behavior of Filter is unwanted
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out _items)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(_items);
	}

	public FlatTreeDataGridSource<CoinGroup> Source { get; }

	public ReadOnlyObservableCollection<CoinGroup> Items => _items;

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private static Func<CoinGroup, bool> SearchItemFilterFunc(string? text)
	{
		return coinGroup =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return true;
			}

			var containsLabel = coinGroup.Labels.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
			return containsLabel;
		};
	}

	public static FlatTreeDataGridSource<CoinGroup> CreateGridSource(IEnumerable<CoinGroup> groups)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Selection		SelectionColumnView		-			Auto		-				-			false
		// Indicators		IndicatorsColumnView	-			Auto		-				-			true
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		// AnonymityScore	AnonymityColumnView		<custom>	50			-				-			true
		// Labels			LabelsColumnView		Labels		*			-				-			true
		var source = new FlatTreeDataGridSource<CoinGroup>(groups)
		{
			Columns =
			{
				// Selection
				new TemplateColumn<CoinGroup>(
					null,
					new FuncDataTemplate<CoinGroup>(
						(node, ns) => new CheckBox
						{
							[!ToggleButton.IsCheckedProperty] = new Binding("IsSelected"),
						},
						true),
					options: new ColumnOptions<CoinGroup>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false
					},
					width: new GridLength(0, GridUnitType.Auto)),
				// Amount
				new TemplateColumn<CoinGroup>(
					"Amount",
					new FuncDataTemplate<CoinGroup>((node, ns) => new TextBlock()
					{
						VerticalAlignment = VerticalAlignment.Center,
						[!TextBlock.TextProperty] = node.TotalAmount.Select(x => MoneyConverters.ToFormattedStringBtc.Convert(x, typeof(string), null, CultureInfo.CurrentUICulture)).ToBinding(),
					}, true),
					options: new ColumnOptions<CoinGroup>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false,
					},
					width: new GridLength(0, GridUnitType.Auto)),
				// Amount
				new TemplateColumn<CoinGroup>(
					"Labels",
					new FuncDataTemplate<CoinGroup>((node, ns) => new TagsBox()
					{
						VerticalAlignment = VerticalAlignment.Center,
						IsReadOnly = true,
						[!TagsBox.ItemsProperty] = new Binding("Labels"),
					}, true),
					options: new ColumnOptions<CoinGroup>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false,
					},
					width: new GridLength(0, GridUnitType.Auto)),
			}
		};

		source.RowSelection!.SingleSelect = true;

		return source;
	}
}
