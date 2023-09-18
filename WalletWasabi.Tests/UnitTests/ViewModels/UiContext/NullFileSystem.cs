using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.FileSystem;

namespace WalletWasabi.Tests.UnitTests.ViewModels.UIContext;

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
