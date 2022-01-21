using NBitcoin;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Coordinator.Banning;

public class BannedUtxo
{
	public BannedUtxo(OutPoint utxo, int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound)
	{
		Utxo = Guard.NotNull(nameof(utxo), utxo);
		Severity = severity;
		TimeOfBan = timeOfBan;
		IsNoted = isNoted;
		BannedForRound = bannedForRound;
	}

	public OutPoint Utxo { get; }
	public int Severity { get; }
	public DateTimeOffset TimeOfBan { get; }
	public bool IsNoted { get; }
	public long BannedForRound { get; }
	public TimeSpan BannedRemaining => DateTimeOffset.UtcNow - TimeOfBan;

	/// <summary>
	/// Deserializes an instance from its text representation.
	/// </summary>
	public static BannedUtxo FromString(string str)
	{
		var parts = str.Split(':');
		var isNoted = bool.Parse(parts[4]);
		var bannedForRound = long.Parse(parts[5]);
		var utxo = new OutPoint(new uint256(parts[3]), int.Parse(parts[2]));
		var severity = int.Parse(parts[1]);
		var timeParts = parts[0].Split('-', ' ').Select(int.Parse).ToArray();
		var timeOfBan = new DateTimeOffset(timeParts[0], timeParts[1], timeParts[2], timeParts[3], timeParts[4], timeParts[5], TimeSpan.Zero);

		return new BannedUtxo(utxo, severity, timeOfBan, isNoted, bannedForRound);
	}

	/// <summary>
	/// Serializes the instance to its text representation.
	/// </summary>
	public override string ToString()
	{
		return $"{TimeOfBan:yyyy-MM-dd HH-mm-ss}:{Severity}:{Utxo.N}:{Utxo.Hash}:{IsNoted}:{BannedForRound}";
	}
}
