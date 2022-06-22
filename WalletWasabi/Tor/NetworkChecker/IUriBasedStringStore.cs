using System.Threading.Tasks;

namespace WalletWasabi.Tor.NetworkChecker;

public interface IUriBasedStringStore
{
	Task<string> Fetch(Uri uri);
}
