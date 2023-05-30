using NBitcoin;

namespace WalletWasabi.Fluent.Models;

public readonly struct DataLegend
{
	public DataLegend(Money amount, string label, string hexColor, double percentShare)
	{
		Amount = amount;
		Label = label;
		HexColor = hexColor;
		PercentShare = percentShare;
	}

	public Money Amount { get; }
	public string Label { get; }
	public string HexColor { get; }
	public double PercentShare { get; }
}
