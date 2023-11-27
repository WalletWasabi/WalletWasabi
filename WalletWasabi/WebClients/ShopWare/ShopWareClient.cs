using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.WebClients.ShopWare;

public record BillingAddress
(
	string Street,
	string AdditionalAddressLine1,
	string Zipcode,
	string City,
	string CountryId
);

public record CustomerRegistrationRequest
(
	string SalutationId,
	string FirstName,
	string LastName,
	string Email,
	bool Guest,
	string AffiliateCode,
	bool AcceptedDataProtection,
	string StorefrontUrl,
	BillingAddress BillingAddress
);

public record CustomerRegistrationResponse
(
	string Id,
	string CustomerNumber
);

public class ShopWareApiClient
{
    private readonly HttpClient httpClient;
    private readonly string apiUrl;
    private readonly string apiKey;

    public ShopWareApiClient(HttpClient client, string apiKey, string? ctxToken = null)
    {
        this.apiUrl = apiUrl;
        this.apiKey = apiKey;
        _client = client;
        _ctxTokens = ctxToken is null ? Array.Empty<string>() : new []{ ctxToken };
        // Initialize HttpClient with required headers
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
        _client.DefaultRequestHeaders.Add("Content-Type", "application/json");
        if (_ctxTokens.Length > 0)
		{
			_client.DefaultRequestHeaders.Add("sw-access-key", _ctxTokens[0]);
		}
    }
	private HttpClient _client;
	private string[] _ctxTokens;

	private enum RemoteAction
	{
		RegisterCustomer
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(CustomerRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationRequest, CustomerRegistrationResponse>(RemoteAction.RegisterCustomer, request, cancellationToken);

		private async Task<string> SendAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
		{
			using var content = Serialize(request);
			using var httpRequest = new HttpRequestMessage(HttpMethod.Post, GetUriEndPoint(action));
			httpRequest.Content = content;
			using var response = await _client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
			}

			_ctxTokens = response.Headers.GetValues("sw-access-key").ToArray();
			return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		private async Task SendAndReceiveAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
		{
			await SendAsync(action, request, cancellationToken).ConfigureAwait(false);
		}

		private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
		{
			var jsonString = await SendAsync(action, request, cancellationToken).ConfigureAwait(false);
			return Deserialize<TResponse>(jsonString);
		}

		private static StringContent Serialize<T>(T obj)
		{
			string jsonString = JsonConvert.SerializeObject(obj, JsonSerializationOptions.Default.Settings);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}

		private static TResponse Deserialize<TResponse>(string jsonString)
		{
			return JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Default.Settings)
				?? throw new InvalidOperationException("Deserialization error");
		}


	private static string GetUriEndPoint(RemoteAction action) =>
		"store-api/" + action switch
		{
			RemoteAction.RegisterCustomer => "account/register",
			_ => throw new NotSupportedException($"Action '{action}' is unknown and has no endpoint associated.")
		};

}
