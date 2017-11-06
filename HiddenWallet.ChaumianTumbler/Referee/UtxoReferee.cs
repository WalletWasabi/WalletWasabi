using ConcurrentCollections;
using HiddenWallet.ChaumianTumbler.Clients;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Referee
{
	[JsonObject(MemberSerialization.OptIn)]
	public class UtxoReferee
    {
		[JsonProperty(PropertyName = "Utxos")]
		public ConcurrentHashSet<BannedUtxo> Utxos { get; private set; }

		public UtxoReferee()
		{
			Utxos = new ConcurrentHashSet<BannedUtxo>();
		}

		public void BanUtxo(OutPoint utxo)
		{
			// if already banned, return
			if(Utxos.Any(x=>x.Utxo.Hash == utxo.Hash && x.Utxo.N == utxo.N))
			{
				return;
			}

			Utxos.Add(new BannedUtxo
			{
				Utxo = utxo,
				TimeOfBan = DateTimeOffset.UtcNow
			});
		}

		public async Task BanAliceAsync(Alice alice)
		{
			foreach(var utxo in alice.Inputs.Select(x=>x.OutPoint))
			{
				BanUtxo(utxo);
			}

			await ToFileAsync(Global.UtxoRefereePath);
		}

		private int PreviousBlockCount { get; set; }
		public async Task StartAsync(CancellationToken cancel)
		{
			while (true)
			{
				try
				{
					if (cancel.IsCancellationRequested) return;

					int blockCount = await Global.RpcClient.GetBlockCountAsync();
					// purge outdated at every new block
					if(PreviousBlockCount != blockCount)
					{
						var toRemove = new HashSet<BannedUtxo>();
						// remove the expired ones
						foreach (BannedUtxo utxo in Utxos)
						{
							var maxBan = (int)TimeSpan.FromDays(30).TotalMinutes;
							int banLeft = maxBan - (int)((DateTimeOffset.UtcNow - utxo.TimeOfBan).TotalMinutes);
							if(banLeft < 0)
							{
								toRemove.Add(utxo);
							}
						}

						// note: do not check if still utxo, it's faster to iterate this list needlessly than ask the rpc

						if(toRemove.Count > 0)
						{
							foreach (var utxo in toRemove)
							{
								Utxos.TryRemove(utxo);
							}
							await ToFileAsync(Global.UtxoRefereePath);
						}
					}

					await Task.Delay(TimeSpan.FromSeconds(21), cancel).ContinueWith(t => { });
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Ignoring {nameof(UtxoReferee)} exception: {ex}");
				}
			}
		}

		public async Task ToFileAsync(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException(nameof(filePath));

			string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
			await File.WriteAllTextAsync(
				filePath,
				jsonString,
				Encoding.UTF8);
		}

		public async static Task<UtxoReferee> CreateFromFileAsync(string filePath)
		{
			if (filePath == null) throw new ArgumentNullException(nameof(filePath));

			string jsonString = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<UtxoReferee>(jsonString);
		}
	}
}
