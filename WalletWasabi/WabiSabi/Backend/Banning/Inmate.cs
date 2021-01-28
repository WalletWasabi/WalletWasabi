using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Backend.Banning
{
	/// <summary>
	/// A UTXO that's living inside the prison.
	/// </summary>
	public class Inmate
	{
		public Inmate(OutPoint utxo, Punishment punishment, DateTimeOffset started, Guid lastDisruptedRoundId)
		{
			Utxo = utxo;
			Punishment = punishment;
			Started = started;
			LastDisruptedRoundId = lastDisruptedRoundId;
		}

		public OutPoint Utxo { get; }
		public Punishment Punishment { get; }
		public DateTimeOffset Started { get; }
		public Guid LastDisruptedRoundId { get; }

		public TimeSpan TimeSpent => DateTimeOffset.UtcNow - Started;

		public static Inmate FromString(string str)
		{
			var parts = str.Split(':');

			var startedString = parts[0];
			var punishmentString = parts[1];
			var utxoHashString = parts[2];
			var utxoIndexString = parts[3];
			var disruptedRoundIdString = parts[4];

			var started = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startedString));
			var punishment = Enum.Parse<Punishment>(punishmentString);
			var utxo = new OutPoint(new uint256(utxoHashString), int.Parse(utxoIndexString));
			var lastDisruptedRoundId = Guid.Parse(disruptedRoundIdString);

			return new(utxo, punishment, started, lastDisruptedRoundId);
		}

		public override string ToString()
		{
			return $"{Started.ToUnixTimeSeconds()}:{Punishment}:{Utxo.Hash}:{Utxo.N}:{LastDisruptedRoundId}";
		}
	}
}
