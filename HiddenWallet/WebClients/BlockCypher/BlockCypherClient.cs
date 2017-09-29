using HiddenWallet.Models;
using HiddenWallet.WebClients.BlockCypher.Models;
using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.WebClients.BlockCypher
{
    public class BlockCypherClient : IDisposable
    {
        public Network Network { get; }
        private HttpClient HttpClient { get; }
        private SemaphoreSlim Semaphore { get; }

        public BlockCypherClient(Network network, HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            if (handler != null) {
                HttpClient = new HttpClient(handler, disposeHandler);
            }
            HttpClient = new HttpClient();
            Semaphore = new SemaphoreSlim(1, 1); // don't make async requests, linux and mac can fail for no reason
            if (network == Network.Main)
            {
                HttpClient.BaseAddress = new Uri("http://api.blockcypher.com/v1/btc/main");
            }
            else if (network == Network.TestNet)
            {
                HttpClient.BaseAddress = new Uri("http://api.blockcypher.com/v1/btc/test3");
            }
            else throw new NotSupportedException($"{network} is not supported");
        }

        public async Task<BlockCypherGeneralInformation> GetGeneralInformationAsync(CancellationToken cancel)
        {
            await Semaphore.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                HttpResponseMessage response =
                        await HttpClient.GetAsync("", HttpCompletionOption.ResponseContentRead, cancel)
                        .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
                var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

                return new BlockCypherGeneralInformation
                {
                    Name = json.Value<string>("name"),
                    Height = new Height(json.Value<int>("height")),
                    Hash = new uint256(json.Value<string>("hash")),
                    Time = DateTimeOffset.Parse(json.Value<string>("time"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                    LatestUrl = new Uri(json.Value<string>("latest_url"), UriKind.Absolute),
                    PreviousHash = new uint256(json.Value<string>("previous_hash")),
                    PreviousUrl = new Uri(json.Value<string>("previous_url"), UriKind.Absolute),
                    PeerCount = json.Value<int>("peer_count"),
                    UnconfirmedCount = json.Value<long>("unconfirmed_count"),
                    LowFee = new FeeRate(new Money(json.Value<long>("low_fee_per_kb"))),
                    MediumFee = new FeeRate(new Money(json.Value<long>("medium_fee_per_kb"))),
                    HighFee = new FeeRate(new Money(json.Value<long>("high_fee_per_kb"))),
                    LastForkHeight = new Height(json.Value<int>("last_fork_height")),
                    LastForkHash = new uint256(json.Value<string>("last_fork_hash"))
                };
            }
            finally
            {
                Semaphore.SafeRelease();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    HttpClient?.Dispose();
                    Semaphore?.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.

                disposedValue = true;
            }
        }

        // override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BlockCypherClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
