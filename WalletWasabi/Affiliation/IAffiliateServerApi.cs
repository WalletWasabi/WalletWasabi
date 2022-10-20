using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation.Models;

namespace WalletWasabi.Affiliation;

public interface IAffiliateServerApi
{
	Task<GetCoinjoinRequestResponse> GetCoinjoinRequest(GetCoinjoinRequestRequest request, CancellationToken cancellationToken);

	Task<StatusResponse> GetStatus(StatusRequest request, CancellationToken cancellationToken);
}
