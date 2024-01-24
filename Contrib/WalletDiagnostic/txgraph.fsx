#load "common.fsx"

open System
open System.IO
open System.Net.Http
open Common

let args = Environment.GetCommandLineArgs()
let walletname = args[2]
let firstTxId = args[3] 

let getCoins (walletName: string) = 
    walletName
    |> Rpc.getListOfCoins
    |> Array.skipWhile (fun x -> x.Txid <> firstTxId)

let coins = getCoins walletname

let coinsGroupedByTx =
    coins
    |> Array.groupBy (fun x -> x.Txid)

let graphNodes = 
    coinsGroupedByTx
    |> Seq.map ( fun (txid, outs) ->
      let indexes = outs |> Array.map (fun x -> string x.Index) |> String.concat "|"
      let paths = outs |> Array.map (fun x -> x.KeyPath) |> String.concat "|"
      let anonsets = outs |> Array.map (fun x -> x.AnonymitySet) |> String.concat "|"
      let amounts = outs |> Array.map (fun x -> $"<idx{x.Index}>{x.Amount}") |> String.concat "|"
      let addresses = outs |> Array.map (fun x -> x.Address) |> String.concat "|" 
      $"tx{txid} [label=\"Tx Id: {txid[..8]}|{{{{ {{ {{ PrvScore | {anonsets} }} | {{ Path | {paths} }} | {{ Idx | {indexes} }} | {{ Address | {addresses} }} |  {{ Amount | {amounts} }} }}}} }}}}\"]"
    )
    |> List.ofSeq
        
let graphEdges =
    coins 
    |> Array.filter (fun x -> x.SpentBy <> "")
    |> Array.map (fun x -> $"tx{x.Txid}:idx{x.Index} -> tx{x.SpentBy}")
    |> List.ofArray

let allLines =
    graphNodes @ graphEdges
    |> String.concat ";\n"

let str = $"""
digraph G {{
graph [center=1 rankdir=LR; overlap=false; splines=true;];
edge [dir=forward];
node [shape=record; ordering="in" ];
{allLines}
}}"""

Console.WriteLine(str)
