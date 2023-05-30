using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IFileSystem
{
	void OpenFolderInFileExplorer(string dirPath);

	Task OpenFileInTextEditorAsync(string filePath);
}
