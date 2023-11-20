using Avalonia.Data.Converters;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Converters;

public static class FeeRateConverters
{
	public static readonly IValueConverter ToSatoshiPerByte =
		new FuncValueConverter<FeeRate, string>(feeRate => feeRate is not null ? $"{feeRate.SatoshiPerByte} sat/vByte" : "");
}
