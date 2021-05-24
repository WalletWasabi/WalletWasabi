using System;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public abstract partial class TileViewModel : ActivatableViewModel
	{
		private readonly int _smallLayoutIndex;
		private readonly int _normalLayoutIndex;
		private readonly int _wideLayoutIndex;
		[AutoNotify] private ObservableCollection<TilePresetViewModel>? _tilePresets;
		[AutoNotify] private int _tilePresetIndex;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSmallLayout;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isNormalLayout;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWideLayout;

		protected TileViewModel()
		{
			_smallLayoutIndex = 0;
			_normalLayoutIndex = 1;
			_wideLayoutIndex = 2;

			this.WhenAnyValue(x => x.TilePresetIndex)
				.Subscribe(x =>
				{
					SetLayoutFlag(x);
					NotifyPresetChanged();
				});
		}

		public int Column => CurrentTilePreset?.Column ?? 0;

		public int Row => CurrentTilePreset?.Row ?? 0;

		public int ColumnSpan => CurrentTilePreset?.ColumnSpan ?? 1;

		public int RowSpan => CurrentTilePreset?.RowSpan ?? 1;

		public bool IsVisible => CurrentTilePreset?.IsVisible ?? true;

		public TilePresetViewModel? CurrentTilePreset => TilePresets?[TilePresetIndex];

		private void NotifyPresetChanged()
		{
			this.RaisePropertyChanged(nameof(CurrentTilePreset));
			this.RaisePropertyChanged(nameof(Column));
			this.RaisePropertyChanged(nameof(Row));
			this.RaisePropertyChanged(nameof(ColumnSpan));
			this.RaisePropertyChanged(nameof(RowSpan));
			this.RaisePropertyChanged(nameof(IsVisible));
		}

		private void SetLayoutFlag(int layoutIndex)
		{
			IsSmallLayout = layoutIndex == _smallLayoutIndex;
			IsNormalLayout = layoutIndex == _normalLayoutIndex;
			IsWideLayout = layoutIndex == _wideLayoutIndex;
		}
	}
}