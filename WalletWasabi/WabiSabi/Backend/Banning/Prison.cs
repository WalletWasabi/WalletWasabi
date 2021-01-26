using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Banning
{
	/// <summary>
	/// Malicious UTXOs are sent here.
	/// </summary>
	public class Prison
	{
		public Prison(IEnumerable<Inmate> inmates)
		{
			Inmates = inmates.ToDictionary(x => x.Utxo, x => x);
		}

		private Dictionary<OutPoint, Inmate> Inmates { get; }
		public object Lock { get; } = new object();

		/// <summary>
		/// To identify the latest change happened in the prison.
		/// </summary>
		public Guid ChangeId { get; private set; } = Guid.NewGuid();

		public (int noted, int banned) CountInmates()
		{
			lock (Lock)
			{
				return (Inmates.Count(x => x.Value.Punishment == Punishment.Noted), Inmates.Count(x => x.Value.Punishment == Punishment.Banned));
			}
		}

		public void Punish(OutPoint utxo, Punishment punishment, ulong lastDisruptedRoundId)
			=> Punish(new Inmate(utxo, punishment, DateTimeOffset.UtcNow, lastDisruptedRoundId));

		private void Punish(Inmate inmate)
		{
			lock (Lock)
			{
				var utxo = inmate.Utxo;

				// If successfully removed, then it contained it previously, so make the punishment banned and restart its time.
				Inmate inmateToPunish = inmate;
				if (Inmates.Remove(utxo))
				{
					// If it was noted before, then no matter the specified punishment, it must be banned.
					// Both the started and the last disrupted round parameters must be updated.
					inmateToPunish = new Inmate(utxo, Punishment.Banned, inmate.Started, inmate.LastDisruptedRoundId);
				}

				Inmates.Add(utxo, inmateToPunish);

				ChangeId = Guid.NewGuid();
			}
		}

		public bool TryRelease(Inmate inmate)
			=> TryRelease(inmate.Utxo, out _);

		private bool TryRelease(OutPoint utxo, [NotNullWhen(returnValue: true)] out Inmate? inmate)
		{
			lock (Lock)
			{
				if (Inmates.TryGetValue(utxo, out inmate))
				{
					Inmates.Remove(utxo);
					ChangeId = Guid.NewGuid();
					return true;
				}
				else
				{
					return false;
				}
			}
		}

		public IEnumerable<Inmate> ReleaseEligibleInmates(TimeSpan time)
		{
			lock (Lock)
			{
				var released = new List<Inmate>();

				foreach (var inmate in Inmates.Values.ToList())
				{
					if (inmate.TimeSpent > time)
					{
						Inmates.Remove(inmate.Utxo);
						released.Add(inmate);
					}
				}

				if (released.Any())
				{
					ChangeId = Guid.NewGuid();
				}

				return released;
			}
		}

		public bool TryGet(OutPoint utxo, [NotNullWhen(returnValue: true)] out Inmate? inmate)
		{
			lock (Lock)
			{
				return Inmates.TryGetValue(utxo, out inmate);
			}
		}

		public IEnumerable<Inmate> GetInamtes()
		{
			lock (Lock)
			{
				return Inmates.Select(x => x.Value).ToList();
			}
		}
	}
}
