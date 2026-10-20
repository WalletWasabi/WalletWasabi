using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Models.FileSystem;

public class FileSystemModel
{
	public Task OpenFileInTextEditorAsync(string filePath)
	{
		FileHelpers.OpenFileInTextEditor(filePath);
		return Task.CompletedTask;
	}

	public void OpenFolderInFileExplorer(string dirPath)
	{
		IoHelpers.OpenFolderInFileExplorer(dirPath);
	}
}
