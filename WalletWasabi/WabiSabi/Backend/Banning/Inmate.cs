using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Banning;

/// <summary>
/// A UTXO that's living inside the prison.
/// </summary>
public record Inmate(
	OutPoint Utxo,
	Punishment Punishment,
	DateTimeOffset Started,
	uint256 LastDisruptedRoundId,
	bool IsLongBan = false)
{
	public TimeSpan TimeSpent => DateTimeOffset.UtcNow - Started;

	public static Inmate FromString(string str)
	{
		var parts = str.Split(':');

		var startedString = parts[0];
		var punishmentString = parts[1];
		var utxoHashString = parts[2];
		var utxoIndexString = parts[3];
		var disruptedRoundIdString = parts[4];
		var isLongBan = false;
		if (parts.Length > 5)
		{
			isLongBan = bool.Parse(parts[5]);
		}

		var started = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startedString));
		var punishment = Enum.Parse<Punishment>(punishmentString);
		var utxo = new OutPoint(new uint256(utxoHashString), int.Parse(utxoIndexString));
		var lastDisruptedRoundId = uint256.Parse(disruptedRoundIdString);

		return new(utxo, punishment, started, lastDisruptedRoundId, isLongBan);
	}

	public override string ToString()
		=> $"{Started.ToUnixTimeSeconds()}:{Punishment}:{Utxo.Hash}:{Utxo.N}:{LastDisruptedRoundId}:{IsLongBan}";
}
