using System.Threading.Tasks;

namespace WalletWasabi.Interfaces
{
	public interface IConfig
	{
		/// <summary>
		/// The path of the config file.
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
		Task ToFileAsync();

		/// <summary>
		/// Load or create the config if the file path of the config file is set, otherwise throw exception.
		/// </summary>
		Task LoadOrCreateDefaultFileAsync();

		/// <summary>
		/// Check if the config file differs from the config if the file path of the config file is set, otherwise throw exception.
		/// </summary>
		Task<bool> CheckFileChangeAsync();
	}
}
