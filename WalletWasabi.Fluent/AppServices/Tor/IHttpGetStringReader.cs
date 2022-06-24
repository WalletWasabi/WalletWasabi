using System.Threading.Tasks;

namespace WalletWasabi.Fluent.AppServices.Tor;

public interface IHttpGetStringReader
{
	Task<string> Read(Uri uri);
}
