using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HiddenWallet.DataClasses;
using NBitcoin;
using Newtonsoft.Json.Linq;

namespace HiddenWallet.Helpers
{
    internal static class BlockrApi
    {
        internal static Network Network = DataRepository.Main.Network;

        private static string BlockrAddress
            => string.Format("http://{0}btc.blockr.io/api/v1/", (Network == Network.Main ? "" : "t"));

        internal static AddressInfo GetAddressInfoSync(string address)
        {
            return GetAddressInfosAsync(new HashSet<string> {address}).Result.FirstOrDefault();
        }

        internal static HashSet<AddressInfo> GetAddressInfosSync(HashSet<string> addresses)
        {
            return GetAddressInfosAsync(addresses).Result;
        }

        internal static async Task<HashSet<AddressInfo>> GetAddressInfosAsync(HashSet<string> addresses)
        {
            while (true)
            {
                using (var client = new HttpClient())
                {
                    var addressesQueryString = addresses.Aggregate((i, j) => i + "," + j);

                    var request = string.Format("{0}address/info/{1}", BlockrAddress, addressesQueryString);
                    var response = await client.GetAsync(request).ConfigureAwait(false);

                    var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JObject.Parse(result);
                    var status = json["status"];
                    if ((status != null && status.ToString() == "error"))
                    {
                        throw new BlockrException(json);
                    }

                    var addressInfos = new HashSet<AddressInfo>();

                    if (addresses.Count == 1)
                    {
                        var address = addresses.FirstOrDefault();

                        var addressInfo = new AddressInfo(address)
                        {
                            Balance = json["data"].Value<decimal>("balance"),
                            BalanceMultisig = json["data"].Value<decimal>("balance_multisig"),
                            TotalReceived = json["data"].Value<decimal>("totalreceived"),
                            TransactionCount = json["data"].Value<uint>("nb_txs")
                        };

                        addressInfos.Add(addressInfo);
                    }
                    else
                    {
                        foreach (
                            var addressInfo in
                                json["data"].Select(element => new AddressInfo(element.Value<string>("address"))
                                {
                                    Balance = element.Value<decimal>("balance"),
                                    BalanceMultisig = element.Value<decimal>("balance_multisig"),
                                    TotalReceived = element.Value<decimal>("totalreceived"),
                                    TransactionCount = element.Value<uint>("nb_txs")
                                }))
                        {
                            addressInfos.Add(addressInfo);
                        }
                    }
                    return addressInfos;
                }
            }
        }
    }
}