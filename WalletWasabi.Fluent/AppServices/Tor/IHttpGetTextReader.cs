using System.Threading.Tasks;

namespace WalletWasabi.Fluent.AppServices.Tor;

public interface IHttpGetTextReader
{
	Task<string> Read(Uri uri);
}
