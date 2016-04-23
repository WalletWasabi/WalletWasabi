// Contains the base classes, structs, and enums that are used in the 
// project.These may be related to but not necessarily be connected to
// the ones in the data repository.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using Newtonsoft.Json;

namespace HiddenWallet.DataClasses
{
    internal class Main
    {
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        internal class WalletFileStructure
        {
            [JsonConstructor]
            public WalletFileStructure(string seed, string keyCount, string network)
            {
                Seed = seed;
                KeyCount = keyCount;
                Network = network;
            }

            internal WalletFileStructure(string path)
            {
                Load(path);
            }

            internal void Save(string path)
            {
                var walletContentString = JsonConvert.SerializeObject(this);

                File.WriteAllText(path, walletContentString);
            }

            private void Load(string path)
            {
                var walletContentString = File.ReadAllText(path);
                var walletContent = JsonConvert.DeserializeObject<WalletFileStructure>(walletContentString);
                Seed = walletContent.Seed;
                KeyCount = walletContent.KeyCount;
                Network = walletContent.Network;
            }

            public string Seed { get; set; }
            public string KeyCount { get; set; }
            public string Network { get; set; }
        }
    }
}