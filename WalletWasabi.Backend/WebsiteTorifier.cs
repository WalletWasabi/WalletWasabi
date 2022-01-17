using System.IO;
using System.Threading.Tasks;

namespace WalletWasabi.Backend;

public class WebsiteTorifier
{
	public WebsiteTorifier(string rootFolder)
	{
		RootFolder = rootFolder;
		UnversionedFolder = Path.GetFullPath(Path.Combine(RootFolder, "unversioned"));
	}

	public string RootFolder { get; }
	public string UnversionedFolder { get; }

	public async Task CloneAndUpdateOnionIndexHtmlAsync()
	{
		var path = Path.Combine(RootFolder, "index.html");
		var onionPath = Path.Combine(RootFolder, "onion-index.html");

		var content = await File.ReadAllTextAsync(path);

		content = content.Replace("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion", "https://wasabiwallet.io", StringComparison.Ordinal);
		content = content.Replace("icon-colored-onion", "icon-colored-clearnet", StringComparison.Ordinal);
		content = content.Replace("https://blockstream.info", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion", StringComparison.Ordinal);
		content = content.Replace("https://wasabiwallet.io/swagger", "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion/swagger", StringComparison.Ordinal);

		await File.WriteAllTextAsync(onionPath, content);
	}
}
