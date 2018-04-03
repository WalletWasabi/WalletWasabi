
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MagicalCryptoWallet.TorSocks5;

public static class TorHttpClientExtensions
{
    public static async Task<HttpResponseMessage> SendAndRetryAsync(this TorHttpClient client, HttpMethod method, string relativeUri, int retry = 2, HttpContent content = null)
    {
        HttpResponseMessage response = null;
        while(retry-- > 0)
        {
            response = await client.SendAsync(method, relativeUri, content);
            if(response.StatusCode == HttpStatusCode.OK)
            {
                break;
            }
            await Task.Delay(1000);

        }
        return response;
    }
}