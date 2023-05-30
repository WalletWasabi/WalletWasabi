using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;

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
}
