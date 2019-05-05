using NBitcoin;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
	public class UtxoReferee
	{
		private IUtxoProvider _utxoProvider;
		private CcjDenialOfServiceConfig _dosConfig;
		private BannedUtxoRepository _repository;
		private bool _initialized = false;

		public UtxoReferee(BannedUtxoRepository repository, IUtxoProvider utxoProvider, CcjDenialOfServiceConfig dosConfig)
		{
			_utxoProvider = Guard.NotNull(nameof(utxoProvider), utxoProvider);
			_dosConfig = Guard.NotNull(nameof(dosConfig), dosConfig);
			_repository = Guard.NotNull(nameof(repository), repository);
		}

		public async Task BanUtxosAsync(int severity, DateTimeOffset timeOfBan, bool forceNoted, long bannedForRound, params OutPoint[] toBan)
		{
			if (_dosConfig.Severity == 0)
			{
				return;
			}

			await EnsureInitializedAsync();
			foreach (var utxo in toBan)
			{
				BannedUtxoRecord foundElem = null;
				if (_repository.TryGet(utxo, out var fe))
				{
					foundElem = fe;
					bool bannedForTheSameRound = foundElem.BannedForRound == bannedForRound;
					if (bannedForTheSameRound && (!forceNoted || foundElem.IsNoted))
					{
						continue; // We would be simply duplicating this ban.
					}
				}

				var isNoted = true;
				if (forceNoted)
				{
					isNoted = true;
				}
				else
				{
					if (_dosConfig.NoteBeforeBan)
					{
						if (foundElem != null)
						{
							isNoted = false;
						}
					}
					else
					{
						isNoted = false;
					}
				}
				_repository.TryAddOrUpdate(new BannedUtxoRecord(utxo, severity, timeOfBan, isNoted, bannedForRound));

				Logger.LogInfo<UtxoReferee>($"UTXO {(isNoted ? "noted" : "banned")} with severity: {severity}. UTXO: {utxo.N}:{utxo.Hash} for disrupting Round {bannedForRound}.");
			}
			await _repository.SaveChangesAsync();
		}

		public async Task UnbanAsync(OutPoint output)
		{
			await EnsureInitializedAsync();
			if (_repository.TryRemove(output))
			{
				await _repository.SaveChangesAsync();
				Logger.LogInfo<UtxoReferee>($"UTXO unbanned: {output.N}:{output.Hash}.");
			}
		}

		public async Task<BannedUtxoRecord> TryGetBannedAsync(OutPoint outpoint, bool notedToo)
		{
			await EnsureInitializedAsync();
			if (_repository.TryGet(outpoint, out var bannedElem))
			{
				int maxBan = (int)TimeSpan.FromHours(_dosConfig.DurationHours).TotalMinutes;
				int banLeftMinutes = maxBan - (int)bannedElem.BannedRemaining.TotalMinutes;
				if (banLeftMinutes > 0)
				{
					if (bannedElem.IsNoted)
					{
						if (notedToo)
						{
							return new BannedUtxoRecord(outpoint, bannedElem.Severity, bannedElem.TimeOfBan, true, bannedElem.BannedForRound);
						}
						else
						{
							return null;
						}
					}
					else
					{
						return new BannedUtxoRecord(outpoint, bannedElem.Severity, bannedElem.TimeOfBan, false, bannedElem.BannedForRound);
					}
				}
				else
				{
					await UnbanAsync(outpoint);
				}
			}
			return null;
		}

		/// <summary>Method used for testing only</summary>
		public int CountBanned(bool notedToo)
		{
			var banned = _repository.Enumerate();
			if (notedToo)
			{
				return banned.Count();
			}
			else
			{
				return banned.Count(x => !x.IsNoted);
			}
		}

		public void Clear()
		{
			_repository.Clear();
		}

		private async Task LoadAsync()
		{
			foreach(var bannedUtxo in _repository.Enumerate())
			{
				var utxo = await _utxoProvider.GetUtxoAsync(bannedUtxo.Utxo.Hash, (int)bannedUtxo.Utxo.N);

				// Check if inputs are unspent.
				if (utxo is null)
				{
					_repository.TryRemove(bannedUtxo.Utxo);
				}
			}
			await _repository.SaveChangesAsync();
		}

		private async Task EnsureInitializedAsync()
		{
			if(_initialized)
			{
				return;
			}
			await LoadAsync();
			_initialized = true;
		}
	}
}
