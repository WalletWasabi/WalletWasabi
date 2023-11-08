#r "nuget:fstoolkit.errorhandling"
#r "nuget:nbitcoin"
#r "../../WalletWasabi/bin/Debug/net8.0/WalletWasabi.dll"
#r "System.Security.Cryptography.dll"

open System.Security.Cryptography

open System.Text
open NBitcoin
open NBitcoin.RPC
open System
open System.IO
open Newtonsoft.Json
open FsToolkit.ErrorHandling
open WalletWasabi.Affiliation.Models
open WalletWasabi.Affiliation.Serialization
open WalletWasabi.Affiliation.Models.CoinJoinNotification

module CommandLine =
    let tryParseArgument (arg : string) =
        match arg.Split([| '=' |]) with
        | [| k; v |] -> Some (k, v)
        | _ -> None

    let getArg name =
        Environment.GetCommandLineArgs()
        |> Array.map tryParseArgument
        |> Array.choose id
        |> Array.tryFind (fun (argName, _) -> argName = name )
        |> Option.map snd

    let getArgOrDefault name defaultValue =
        getArg name
        |> Option.defaultValue defaultValue

    let getArgOrFail name =
        match getArg name with
        | Some value -> value
        | None -> failwith $"Mandatory argument {name} was not provided"

module Signature =

    let verifySignature (pubkey:byte[]) (message:byte[]) signature =
        let ecdsa = ECDsa.Create()
        let _ = ecdsa.ImportSubjectPublicKeyInfo(ReadOnlySpan(pubkey))
        ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256)

    let verifyNotification affiliate pubkey (notification: CoinJoinNotificationRequest) =
         let payload = Payload(Header.Create(affiliate), notification.Body)
         verifySignature pubkey (payload.GetCanonicalSerialization()) notification.Signature

open Signature
open CommandLine

let args = {|
    rpcCredentials = RPCCredentialString.Parse (getArgOrFail "--connection")
    network = Network.GetNetwork (getArgOrDefault "--network" "Main")
    notificationsPath = getArgOrDefault "--path" "."
    affiliate = getArgOrDefault "--affiliate" "trezor"
    pubkey = Convert.FromHexString(getArgOrFail "--pubkey")
    coordinationFeeRate = decimal (getArgOrDefault "--coordinationFeeRate" "0.003")
|}

let rpc = RPCClient(args.rpcCredentials, args.network)
let notificationFiles = Directory.EnumerateFiles (args.notificationsPath, "*.txt;*.json")

type NotificationProcessError =
     | ApocryphalNotification of CoinJoinNotificationRequest
     | NonexistentTransaction of uint256 * CoinJoinNotificationRequest

let processNotification notification = result {
    do! verifyNotification args.affiliate args.pubkey notification
        |> Result.requireTrue (ApocryphalNotification notification)

    let notificationBody = notification.Body
    let txId = uint256.Parse(notificationBody.TransactionId)
    let! tx =
        rpc.GetRawTransaction(txId, false)
        |> Result.requireNotNull (NonexistentTransaction (txId, notification))

    let affInputs =
        notificationBody.Inputs
        |> Seq.filter (fun x -> x.IsAffiliated && not x.IsNoFee)
        |> Seq.map (fun x -> x.Amount, x.Prevout)
        |> List.ofSeq

    let affInputSum = List.sumBy fst affInputs
    let affShare = int64 (args.coordinationFeeRate * decimal affInputSum)

    return! Ok (txId, affInputs, affInputSum, affShare, notification)
}

let deserializeCoinJoinNotificationRequest s =
    JsonConvert.DeserializeObject<CoinJoinNotificationRequest>(s, AffiliationJsonSerializationOptions.Settings)

let notificationProcessResult =
    notificationFiles
    |> Seq.map File.ReadAllText
    |> Seq.map deserializeCoinJoinNotificationRequest
    |> Seq.map processNotification

notificationProcessResult
|> Seq.zip notificationFiles
|> Seq.iter (
    function
    | _, Ok (txId, affInputs, affInputSum, affShare, _) ->
        Console.WriteLine $"coinjoin: {txId} - Total amount: {affInputSum} satoshis. Share: {affShare}"
        affInputs
        |> List.iter (fun (amount, prevout) -> Console.WriteLine $"          {amount} {prevout.Hash |> Convert.ToHexString}:{prevout.Index}")
    | file, Error (ApocryphalNotification notificationRequest) ->
        Console.WriteLine $"File {file} contains an apocryphal notification request"
    | file, Error (NonexistentTransaction (txId, notificationRequest)) ->
        Console.WriteLine $"File {file} contains a notification for coinjoin '{txId}' which is not in the blockchain"
    )

let totalToShare =
    notificationProcessResult
    |> Seq.choose Result.toOption
    |> Seq.sumBy (fun (_, _, _, shared, _) -> shared)

Console.WriteLine $"Total revenue to share: {Money.Satoshis(totalToShare)} btc."
