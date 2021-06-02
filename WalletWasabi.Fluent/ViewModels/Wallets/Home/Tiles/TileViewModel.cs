using System;
using System.Collections.ObjectModel;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public abstract partial class TileViewModel : ActivatableViewModel
	{
		[AutoNotify] private ObservableCollection<TilePresetViewModel>? _tilePresets;
		[AutoNotify] private int _tilePresetIndex;
		[AutoNotify] private TileSize _currentTileSize;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSmallPreset;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isNormalPreset;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWidePreset;


		protected TileViewModel()
		{
			this.WhenAnyValue(x => x.TilePresetIndex)
				.Subscribe(_ =>
				{
					UpdatePresetFlags();
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

		private void UpdatePresetFlags()
		{
			CurrentTileSize = CurrentTilePreset?.TileSize ?? TileSize.Normal;

			IsSmallPreset = CurrentTilePreset?.TileSize == TileSize.Small;
			IsNormalPreset = CurrentTilePreset?.TileSize == TileSize.Normal;
			IsWidePreset = CurrentTilePreset?.TileSize == TileSize.Wide;
		}
	}
}