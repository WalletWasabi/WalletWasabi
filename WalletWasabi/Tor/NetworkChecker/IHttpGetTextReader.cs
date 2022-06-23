using System.Threading.Tasks;

namespace WalletWasabi.Tor.NetworkChecker;

public interface IHttpGetTextReader
{
	Task<string> Read(Uri uri);
}
