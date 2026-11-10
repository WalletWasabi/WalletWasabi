using System.IO;

namespace WalletWasabi.Storages;

public static class SqliteStorageHelper
{
	/// <remarks>Useful for testing purposes to create a non-persistent SQLite database in memory.</remarks>
	/// <seealso href="https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases"/>
	public const string InMemoryDatabase = ":memory:";

	public static void DeleteDatabaseFiles(string mainFilePath)
	{
		if (File.Exists(mainFilePath))
		{
			File.Delete(mainFilePath);
		}

		if (File.Exists($"{mainFilePath}-shm"))
		{
			File.Delete($"{mainFilePath}-shm");
		}

		if (File.Exists($"{mainFilePath}-wal"))
		{
			File.Delete($"{mainFilePath}-wal");
		}
	}
}
