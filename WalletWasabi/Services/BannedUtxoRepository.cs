using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
	/// <summary>
	/// BannedUtxoRepository provides access to the stored utxo from disk.
	/// </summary>
	/// <remarks>
	/// It abstracts the access to the stored utxo from file system and keeps the 
	/// items in an in memory (internal cache) for fast access.
	/// All changes to the internal in-memory list need to be persisted by calling
	/// SaveChangesAsync() method.
	/// </remarks>
	public class BannedUtxoRepository
	{
		private ConcurrentDictionary<OutPoint, BannedUtxoRecord> _bannedUtxos; // in-memory cache
		private string _filePath;
		private bool _initialized = false;
		private bool _isDirty = false;		// indicates whether changes are in memory but not persisted yet.

		public BannedUtxoRepository(string filePath)
		{
			_filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
			_bannedUtxos = new ConcurrentDictionary<OutPoint, BannedUtxoRecord>();
		}

		/// <summary>
		/// TryAddOrUpdate tries to replace an existing item or add it in case it is not already banned.
		/// </summary>
		/// <remarks>
		/// TryAddOrUpdate tries to replace an existing item with the one passes or add it in case
		/// the coin is not banned already.
		/// This acts on the in-memory cache only and SaveChangesAsync has to be called to persist the
		/// changes to disk.
		/// </remarks>
		/// <param name="utxo">The BannedUtxoRecord record containing the coin banning info.</param>
		/// <returns>True if was able to update or add the item; otherwise false.</returns>
		public bool TryAddOrUpdate(BannedUtxoRecord utxo)
		{
			EnsureInitialized();
			if(_bannedUtxos.ContainsKey(utxo.Utxo))
			{
				_bannedUtxos[utxo.Utxo] = utxo;
				return true;
			}
			else if(_bannedUtxos.TryAdd(utxo.Utxo, utxo))
			{
				_isDirty = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// TryRemove tries to remove an item..
		/// </summary>
		/// <remarks>
		/// TryRemove tries to remove an item from the internal cache. 
		/// SaveChangesAsync has to be called to persist the changes to disk.
		/// </remarks>
		/// <param name="utxo">The outpoint which has to be removed from the banned coin list.</param>
		/// <returns>True if was able to remove the item; otherwise false.</returns>
		public bool TryRemove(OutPoint utxo)
		{
			EnsureInitialized();
			if(_bannedUtxos.TryRemove(utxo, out var _))
			{
				_isDirty = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// TryGet tries to return an item from the in-memory cache.
		/// </summary>
		/// <remarks>
		/// TryGet tries to return the requested item from the internal cache.
		/// </remarks>
		/// <param name="key">The outpoint we want to search in the banned coin list.</param>
		/// <param name="bannedUtxo">The BannedUtxoRecord record containing the coin banning info.</param>
		/// <returns>True if was able to find the item; otherwise false.</returns>
		public bool TryGet(OutPoint key, out BannedUtxoRecord bannedUtxo)
		{
			EnsureInitialized();
			return _bannedUtxos.TryGetValue(key, out bannedUtxo);
		}

		/// <summary>
		/// SaveChangesAsync persists the changes to disk.
		/// </summary>
		/// <remarks>
		/// Dumps the serialized representation of the in-memory cache to the file system only
		/// if there are changes that need to be saved.
		/// </remarks>
		public async Task SaveChangesAsync()
		{
			if(_isDirty)
			{
				EnsureInitialized();
				var lines = _bannedUtxos.OrderBy(x=>x.Value.TimeOfBan).Select(x=>x.Value.ToString());
				await File.WriteAllLinesAsync(_filePath, lines);
				_isDirty = false;
			}
		}

		/// <summary>
		/// Enumerates the current state of the internal cache.
		/// </summary>
		public IEnumerable<BannedUtxoRecord> Enumerate()
		{
			EnsureInitialized();
			foreach(var record in _bannedUtxos)
			{
				yield return record.Value;
			}
		}

		/// <summary>
		/// Drops the content of the internal cache and deletes the file.
		/// </summary>
		public void Clear()
		{
			_bannedUtxos.Clear();
			_isDirty = false;
			File.Delete(_filePath);
		}

		/// <summary>
		/// Ensures to load the list of banned coins from disk before to do anything else.
		/// </summary>
		private void EnsureInitialized()
		{
			if(_initialized)
			{
				return;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

			Load();
			_initialized = true;
		}

		/// <summary>
		/// Loads the serialized banned coins from the file system.
		/// </summary>
		/// <remarks>
		/// This method is invoked automatically before any other action is performed. 
		/// In case the file is corrupted this method stops processing it and deletes 
		/// the file. 
		/// </remarks>
		private void Load()
		{
			try
			{
				using(var file = new StreamReader(_filePath))
				{
					string line;
					while((line = file.ReadLine()) != null)
					{
						var record = BannedUtxoRecord.FromString(line);
						_bannedUtxos.TryAdd(record.Utxo, record);
					}
				}
				Logger.LogInfo<UtxoReferee>($"{_bannedUtxos.Count()} banned UTXOs are loaded from {_filePath}.");
			}
			catch(FileNotFoundException)
			{
				Logger.LogInfo<UtxoReferee>($"No banned UTXOs are loaded from {_filePath}.");
			}
			catch(Exception ex)
			{
				Logger.LogWarning<UtxoReferee>($"Banned UTXO file got corrupted. Deleting {_filePath}. {ex.GetType()}: {ex.Message}");
				Clear();
			}
		}
	}
}
