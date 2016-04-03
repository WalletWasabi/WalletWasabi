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

                    var requestAddressInfo = string.Format("{0}address/info/{1}", BlockrAddress, addressesQueryString);
                    var requestAddressUnspent = string.Format("{0}address/unspent/{1}", BlockrAddress, addressesQueryString);

                    var responseAddressInfo = await client.GetAsync(requestAddressInfo).ConfigureAwait(false);
                    var responseAddressUnspent = await client.GetAsync(requestAddressUnspent).ConfigureAwait(false);

                    var resultAddressInfo = await responseAddressInfo.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var resultAddressUnspent = await responseAddressUnspent.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var jsonAddressInfo = JObject.Parse(resultAddressInfo);
                    var jsonAddressUnspent = JObject.Parse(resultAddressUnspent);

                    CheckStatus(jsonAddressInfo);
                    CheckStatus(jsonAddressUnspent);

                    var addressInfos = new HashSet<AddressInfo>();

                    if (addresses.Count == 1)
                    {
                        var address = addresses.FirstOrDefault();

                        var transactions = new HashSet<TransactionInfo>();

                        foreach (var transaction in jsonAddressUnspent["data"]["unspent"])
                        {
                            transactions.Add(new TransactionInfo(
                                transaction.Value<string>("tx"),
                                transaction.Value<decimal>("amount"),
                                transaction.Value<uint>("confirmations"),
                                jsonAddressUnspent["data"].Value<string>("address")
                                ));
                        }

                        var addressInfo = new AddressInfo(address)
                        {
                            Balance = jsonAddressInfo["data"].Value<decimal>("balance"),
                            BalanceMultisig = jsonAddressInfo["data"].Value<decimal>("balance_multisig"),
                            TotalReceived = jsonAddressInfo["data"].Value<decimal>("totalreceived"),
                            TransactionCount = jsonAddressInfo["data"].Value<uint>("nb_txs"),
                            UnspentTransactions = new HashSet<TransactionInfo>(transactions)
                        };

                        addressInfos.Add(addressInfo);
                    }
                    else
                    {
                        foreach (var element in jsonAddressInfo["data"])
                        {
                            var addressInfo = new AddressInfo(element.Value<string>("address"))
                            {
                                Balance = element.Value<decimal>("balance"),
                                BalanceMultisig = element.Value<decimal>("balance_multisig"),
                                TotalReceived = element.Value<decimal>("totalreceived"),
                                TransactionCount = element.Value<uint>("nb_txs")
                            };
                            addressInfos.Add(addressInfo);
                        }

                        foreach (var element in jsonAddressUnspent["data"])
                        {
                            var transactions = new HashSet<TransactionInfo>();

                            foreach (var transaction in element["unspent"])
                            {
                                transactions.Add(new TransactionInfo(
                                    transaction.Value<string>("tx"),
                                    transaction.Value<decimal>("amount"),
                                    transaction.Value<uint>("confirmations"),
                                    element.Value<string>("address")
                                    ));
                            }

                            foreach (var addressInfo in addressInfos)
                            {
                                if (addressInfo.Address == element.Value<string>("address"))
                                {
                                    addressInfo.UnspentTransactions = new HashSet<TransactionInfo>(transactions);
                                }
                            }
                        }
                    }
                    return addressInfos;
                }
            }
        }

        private static void CheckStatus(JObject json)
        {
            var status = json["status"];
            if ((status != null && status.ToString() == "error"))
            {
                throw new BlockrException(json);
            }
        }

        internal static string PushTransactionSync(string transactionHash)
        {
            return PushTransactionAsync(transactionHash).Result;
        }

        internal static async Task<string> PushTransactionAsync(string transactionHash)
        {
            while (true)
            {
                using (var client = new HttpClient())
                {
                    var request = string.Format("{0}tx/push", BlockrAddress);
                    
                    var content = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("hex", transactionHash)
                        });

                    var response = await client.PostAsync(request, content).ConfigureAwait(false);

                    return response.Content.ReadAsStringAsync().Result;
                }
            }
        }
    }
}