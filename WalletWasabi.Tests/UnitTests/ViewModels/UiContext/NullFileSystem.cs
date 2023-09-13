using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.FileSystem;

public class NullFileSystem : IFileSystem
{
	public Task OpenFileInTextEditorAsync(string filePath)
	{
		return Task.CompletedTask;
	}

	public void OpenFolderInFileExplorer(string dirPath)
	{
	}

	public Task OpenBrowserAsync(string url)
	{
		return Task.CompletedTask;
	}
}
