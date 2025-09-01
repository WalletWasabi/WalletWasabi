open System
open System.Net.WebSockets
open System.Threading
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Control
open NBitcoin.Secp256k1
open Nostra
open Nostra.Client
open System.IO

let publishNote note (relay: Uri) =
    let ws = new ClientWebSocket()
    let ctx = Communication.buildContext ws Console.Out
    let pushToRelay = Monad.injectedWith ctx (Communication.sender ())
    async {
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds 10)
        try
            do! ws.ConnectAsync(relay, cts.Token) |> Async.AwaitTask
            pushToRelay (Request.CMEvent note)
            do! Async.Sleep (TimeSpan.FromSeconds 8)
            return Ok relay
        with ex ->
            return Error (relay, ex.Message)
    }

let buildMessage content (tags : (string * string list) list) =
    let links = tags |> List.map (fun (_, urls) -> List.head urls)
    let linksSection = String.Join ("\n", links)
    $"{content}\n\n# Links\n\n{linksSection}"

let buildMessageTags version baseUri =
    [
        "SHA256SUMS"; "SHA256SUMS.asc"; "SHA256SUMS.wasabisig";
        $"Wasabi-{version}-arm64.dmg"; $"Wasabi-{version}-arm64.dmg.asc";
        $"Wasabi-{version}-linux-x64.tar.gz"; $"Wasabi-{version}-linux-x64.tar.gz.asc";
        $"Wasabi-{version}-linux-x64.zip"; $"Wasabi-{version}-linux-x64.zip.asc";
        $"Wasabi-{version}-macOS-arm64.zip"; $"Wasabi-{version}-macOS-arm64.zip.asc";
        $"Wasabi-{version}-macOS-x64.zip"; $"Wasabi-{version}-macOS-x64.zip.asc";
        $"Wasabi-{version}-win-x64.zip"; $"Wasabi-{version}-win-x64.zip.asc";
        $"Wasabi-{version}.deb"; $"Wasabi-{version}.deb.asc";
        $"Wasabi-{version}.dmg"; $"Wasabi-{version}.dmg.asc";
        $"Wasabi-{version}.msi"; $"Wasabi-{version}.msi.asc"
    ] |> List.map (fun file -> file, [$"{baseUri}/{file}"])


[<EntryPoint>]
let main args =
    if args.Length < 3 then
       printfn "Usage: program <version> <content-file-path> <private-key-hex>"
       1
    else
        let version = args[0]
        let content = File.ReadAllText args[1]
        let secretHex = args[2] |> Utils.fromHex |> ECPrivKey.Create
        let tags = ["version", [version]] @ buildMessageTags version $"https://github.com/WalletWasabi/WalletWasabi/releases/download/v{version}"
        let finalContent = buildMessage content tags
        let unsignedNote = Event.createEvent Event.Kind.Text tags finalContent
        let note = Event.sign secretHex unsignedNote

        [
            Uri "wss://relay.nostrified.org"
            Uri "wss://relay.nostromo.social"
            Uri "wss://nostr.l00p.org"
            Uri "wss://relay.damus.io"
            Uri "wss://relay.primal.net"
            Uri "wss://nostr.mom"
            Uri "wss://nos.lol"
            Uri "wss://purplerelay.com"
            Uri "wss://eden.nostr.land"
        ]
        |> List.map (publishNote note)
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.map (function
                     | Ok relay -> $"✅ Successfully published to {relay}"
                     | Error (relay, msg) -> $"❌ Failed to publish to {relay} {msg}")
        |> Array.iter Console.WriteLine
        0
