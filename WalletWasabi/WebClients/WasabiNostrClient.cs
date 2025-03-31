using NNostr.Client;
using NNostr.Client.Protocols;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Discoverability;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients;
public class WasabiNostrClient : IDisposable
{
	private const string DefaultPublicKey = "npub1l0p8r79n24ez6ahh93utyyu268hj7cg3gdsql4526rwlc6qhxx3sxy0yeu"; // Change this to Official Wasabi Nostr PubKey
	private EndPoint _torEndpoint;
	private string? _nostrSubscriptionID;

	public WasabiNostrClient(EndPoint endPoint)
	{
		_torEndpoint = endPoint;
	}

	public INostrClient? NostrWebClient { get; set; }
	public Dictionary<string, NostrEvent> Events { get; set; } = new();
	public Channel<NostrUpdateAssets> UpdateChannel { get; set; } = Channel.CreateUnbounded<NostrUpdateAssets>();

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId == _nostrSubscriptionID)
		{
			foreach (NostrEvent nostrEvent in args.events)
			{
				try
				{
					if (Events.TryAdd(nostrEvent.Id, nostrEvent))
					{
						List<Asset> assets = new List<Asset>();
						Version version = new Version(nostrEvent.Tags.First(tag => tag.TagIdentifier == "version").Data.First()) ?? throw new InvalidCastException("Version tag of nostr event couldn't be casted.");
						/*
						Asset arm64 = new("arm64.dmg", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "arm64.dmg").Data.First()));
						Asset arm64Asc = new("arm64.dmg.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "arm64.dmg.asc").Data.First()));
						assets.Add(arm64);
						assets.Add(arm64Asc);

						Asset dmg = new(".dmg", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == ".dmg").Data.First()));
						Asset dmgAsc = new(".dmg.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == ".dmg.asc").Data.First()));
						assets.Add(dmg);
						assets.Add(dmgAsc);

						// Update Manager doesn't use these .zip files, maybe we can ignore them in the Nostr event?
						Asset macOSarm64 = new("macOS-arm64.zip", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "macOS-arm64.zip").Data.First()));
						Asset macOSarm64Asc = new("macOS-arm64.zip.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "macOS-arm64.zip.asc").Data.First()));
						assets.Add(macOSarm64);
						assets.Add(macOSarm64Asc);

						Asset macOSX64 = new("macOS-x64.zip", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "macOS-x64.zip").Data.First()));
						Asset macOSX64Asc = new("macOS-x64.zip.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "macOS-x64.zip.asc").Data.First()));
						assets.Add(macOSX64);
						assets.Add(macOSX64Asc);

						Asset linuxTarGz = new("linux.x64.tar.gz", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "linux.x64.tar.gz").Data.First()));
						Asset linuxTarGzAsc = new("linux.x64.tar.gz.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "linux.x64.tar.gz.asc").Data.First()));
						assets.Add(linuxTarGz);
						assets.Add(linuxTarGzAsc);

						Asset linuxZip = new("linux.x64.zip", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "linux.x64.zip").Data.First()));
						Asset linuxZipAsc = new("linux.x64.zip.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "linux.x64.zip.asc").Data.First()));
						assets.Add(linuxZip);
						assets.Add(linuxZipAsc);

						Asset linuxDeb = new(".deb", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == ".deb").Data.First()));
						Asset linuxDebAsc = new(".deb.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == ".deb.asc").Data.First()));
						assets.Add (linuxDeb);
						assets.Add(linuxDebAsc);

						

						
						*/

						Asset msi = new(".msi", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == ".msi").Data.First()));
						Asset msiAsc = new(".msi.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == ".msi.asc").Data.First()));
						assets.Add(msi);
						assets.Add(msiAsc);
						/*
						Asset winX64 = new("win-x64.zip", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "win-x64.zip").Data.First()));
						Asset winX64Asc = new("win-x64.zip.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "win-x64.zip.asc").Data.First()));
						assets.Add(winX64);
						assets.Add(winX64Asc);

						Asset sha256sums = new("SHA256SUMS", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "SHA256SUMS").Data.First()));
						Asset sha256sumsAsc = new("SHA256SUMS.asc", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "SHA256SUMS.asc").Data.First()));
						Asset sha256sumsWasabisig = new("SHA256SUMS.wasabisig", new Uri(nostrEvent.Tags.First(tag => tag.TagIdentifier == "SHA256SUMS.wasabisig").Data.First()));
						assets.Add(sha256sums);
						assets.Add(sha256sumsAsc);
						assets.Add(sha256sumsWasabisig);
						*/

						NostrUpdateAssets newUpdate = new(version, assets);
						UpdateChannel.Writer.TryWrite(newUpdate);
					}
				}
				catch (Exception ex) when (ex is InvalidOperationException || ex is InvalidCastException || ex is ArgumentException)
				{
					Logger.LogError($"Invalid Nostr Event received. ID: {nostrEvent.Id}");
				}
			}
		}
	}

	public async Task InitializeNostrConnectionAsync(CancellationToken cancel)
	{
		try
		{
			string defaultPubKeyHex = NIP19.FromNIP19Npub(DefaultPublicKey).ToHex();

			string[] relayUrls = ["wss://relay.primal.net", "wss://nos.lol", "wss://relay.damus.io"];
			Uri[] uris = relayUrls.Select(x => new Uri(x)).ToArray();
			NostrWebClient = NostrClientFactory.Create(uris, _torEndpoint);
			NostrWebClient.EventsReceived += OnNostrEventsReceived;

			await NostrWebClient.ConnectAndWaitUntilConnected(cancel).ConfigureAwait(false);

			_nostrSubscriptionID = Guid.NewGuid().ToString();
			await NostrWebClient.CreateSubscription(_nostrSubscriptionID, [new() { Kinds = [1], Authors = [defaultPubKeyHex] }], cancel).ConfigureAwait(false);

		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			NostrWebClient?.Dispose();
		}
	}

	public void Dispose()
	{
		if (NostrWebClient is null)
		{
			return;
		}

		NostrWebClient.EventsReceived -= OnNostrEventsReceived;
		NostrWebClient.Dispose();
	}

	public record NostrUpdateAssets(Version Version, List<Asset> Assets);
	public record Asset(string Name, Uri DownloadUri);
}
