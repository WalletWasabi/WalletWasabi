namespace WalletWasabi.Interfaces;

public interface IConfig
{
	/// <summary>
	/// Gets the path of the config file.
	/// </summary>
	string FilePath { get; }

	/// <summary>
	/// Set the path of the config file.
	/// </summary>
	void SetFilePath(string path);

	/// <summary>
	/// Throw exception if the path of the config file is not set.
	/// </summary>
	void AssertFilePathSet();

	/// <summary>
	/// Serialize the config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	void ToFile();

	/// <summary>
	/// Load or create the config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	void LoadOrCreateDefaultFile();

	/// <summary>
	/// Load config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	void LoadFile();

	bool AreDeepEqual(object otherConfig);

	/// <summary>
	/// Check if the config file differs from the config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	bool CheckFileChange();
}
