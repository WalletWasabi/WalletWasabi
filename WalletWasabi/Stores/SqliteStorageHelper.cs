namespace WalletWasabi.Stores;

public static class SqliteStorageHelper
{
	/// <remarks>Useful for testing purposes to create a non-persistent SQLite database in memory.</remarks>
	/// <seealso href="https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases"/>
	public const string InMemoryDatabase = ":memory:";
}
