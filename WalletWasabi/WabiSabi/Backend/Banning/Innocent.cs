using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public record Innocent(
	OutPoint Outpoint,
	DateTimeOffset Started)
{
	public TimeSpan TimeSpent => DateTimeOffset.UtcNow - Started;

	public static bool TryReadFromString(string str, [NotNullWhen(true)] out Innocent? innocent)
	{
		try
		{
			var parts = str.Split(':');
			if (parts.Length != 3)
			{
				throw new FormatException($"Innocent string was invalid. It was '{str}'");
			}

			var startedString = parts[0];
			var utxoHashString = parts[1];
			var utxoIndexString = parts[2];

			var started = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startedString));
			var outpoint = new OutPoint(new uint256(utxoHashString), int.Parse(utxoIndexString));
			innocent = new(outpoint, started);
			return true;
		}
		catch (Exception exc)
		{
			Logger.LogWarning(exc);
			innocent = null;
			return false;
		}
	}
	public override string ToString()
		=> $"{Started.ToUnixTimeSeconds()}:{Outpoint.Hash}:{Outpoint.N}";
}
