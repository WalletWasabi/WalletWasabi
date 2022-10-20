using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation.Models;

namespace WalletWasabi.Affiliation;

public interface IAffiliateServerApi
{
	Task<PaymentDataResponse> GetPaymentData(PaymentDataRequest request, CancellationToken cancellationToken);

	Task<StatusResponse> GetStatus(StatusRequest request, CancellationToken cancellationToken);
}
