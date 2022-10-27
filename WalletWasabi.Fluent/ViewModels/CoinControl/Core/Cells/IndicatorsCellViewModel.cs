namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core.Cells;

public class IndicatorsCellViewModel : ViewModelBase
{
	public IndicatorsCellViewModel(ICoin coin)
	{
		IsCoinjoining = coin.IsCoinjoining;
		IsConfirmed = coin.IsConfirmed;
		IsBanned = coin.BannedUntil.HasValue;
		ConfirmationStatus = coin.IsConfirmed ? "Confirmed" : "Pending confirmation";
		BannedUntilUtcToolTip = coin.BannedUntil.HasValue ? $"Can't participate in coinjoin until: {coin.BannedUntil:g}" : "";
	}

	public bool IsBanned { get; }

	public string BannedUntilUtcToolTip { get; }

	public bool IsCoinjoining { get; }

	public bool IsConfirmed { get; }

	public string? ConfirmationStatus { get; }
}
