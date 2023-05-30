using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Models.FileSystem;

public class FileSystemModel : IFileSystem
{
	public Task OpenFileInTextEditorAsync(string filePath)
	{
		return FileHelpers.OpenFileInTextEditorAsync(filePath);
	}

	public void OpenFolderInFileExplorer(string dirPath)
	{
		IoHelpers.OpenFolderInFileExplorer(dirPath);
	}
}
