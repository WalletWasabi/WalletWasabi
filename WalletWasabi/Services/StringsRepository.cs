using System;
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
	/// StringRepository provides access to the stored items from disk.
	/// </summary>
	/// <remarks>
	/// It abstracts the access to a collection of stored strings from file system and keeps the 
	/// items in an in memory (internal cache) for fast access.
	/// All changes to the internal in-memory list need to be persisted by calling
	/// SaveChangesAsync() method.
	/// </remarks>
	public class StringsRepository
	{
		private ConcurrentHashSet<string> _inMemoryCache; // in-memory cache
		private string _filePath;
		private bool _initialized = false;
		private bool _isDirty = false;		// indicates whether changes are in memory but not persisted yet.

		public StringsRepository(string filePath)
		{
			_filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
			_inMemoryCache = new ConcurrentHashSet<string>();
		}

		/// <summary>
		/// TryAdd tries to add an item to the repository.
		/// </summary>
		/// <remarks>
		/// TryAdd tries to add an item to the repository.
		/// This acts on the in-memory cache only and SaveChangesAsync has to be called to persist the
		/// changes to disk.
		/// </remarks>
		/// <param name="data">The data item to persist.</param>
		/// <returns>True if the item was added; otherwise false.</returns>
		public bool TryAdd(string data)
		{
			EnsureInitialized();
			if(_inMemoryCache.TryAdd(data))
			{
				_isDirty = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Exists checks for the existance of a given string in the in-memory cache.
		/// </summary>
		/// <param name="data">The data item we want to search in the repository.</param>
		/// <returns>True if was able to find the item; otherwise false.</returns>
		public bool Exists(string data)
		{
			EnsureInitialized();
			return _inMemoryCache.Contains(data);
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
				await File.WriteAllLinesAsync(_filePath, _inMemoryCache);
				_isDirty = false;
			}
		}

		/// <summary>
		/// Enumerates the current state of the internal cache.
		/// </summary>
		public IEnumerable<string> Enumerate()
		{
			EnsureInitialized();
			return _inMemoryCache.AsEnumerable();
		}

		/// <summary>
		/// Drops the content of the internal cache and deletes the file.
		/// </summary>
		public void Clear()
		{
			_inMemoryCache.Clear();
			_isDirty = false;
			File.Delete(_filePath);
		}

		/// <summary>
		/// Ensures to load the list from disk before to do anything else.
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
		/// Loads the serialized addresses from the file system.
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
						_inMemoryCache.TryAdd(line);
					}
				}
				Logger.LogInfo<StringsRepository>($"{_inMemoryCache.Count()} items are loaded from {_filePath}.");
			}
			catch(FileNotFoundException)
			{
				Logger.LogInfo<StringsRepository>($"No data loaded from {_filePath}.");
			}
			catch(Exception ex)
			{
				Logger.LogWarning<StringsRepository>($"File got corrupted. Deleting {_filePath}. {ex.GetType()}: {ex.Message}");
				Clear();
			}
		}
	}
}
