namespace WalletWasabi.Interfaces;

public interface IConfig
{
	/// <summary>Gets the path of the config file.</summary>
	string FilePath { get; }

	/// <summary>Set the path of the config file.</summary>
	void SetFilePath(string path);

	/// <summary>Throw exception if the path of the config file is not set.</summary>
	void AssertFilePathSet();

	/// <summary>Serialize the config if the file path of the config file is set, otherwise throw exception.</summary>
	void ToFile();

	/// <summary>Load config from configuration file.</summary>
	/// <param name="createIfMissing"><c>true</c> if the config file should be created if it does not exist, <c>false</c> otherwise.</param>
	void LoadFile(bool createIfMissing = false);
}
