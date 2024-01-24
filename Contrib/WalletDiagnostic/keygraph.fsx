#r "nuget:Plotly.NET"
#r "nuget:Plotly.NET.ImageExport"
#load "common.fsx"

open System
open System.IO
open Plotly.NET 
open Plotly.NET.ImageExport
open System.Net.Http
open FSharp.Data
open Common 

type KeyInfo = { Path: string; State: int }


let args = Environment.GetCommandLineArgs()
let walletname = args[2]

let getKeys (walletName: string) = 
    walletName
    |> Rpc.getListOfKeys 
    |> Array.map (fun x -> { Path=x.FullKeyPath; State=x.KeyState })
    |> List.ofArray

let filterByKeyPath (keys: KeyInfo list) (kp: string) =
    keys
    |> List.filter (fun k -> k.Path.StartsWith kp)
    |> List.sortBy (fun k -> int <| Array.last (k.Path.Split [|'/'|]))

let stateToHeat (key: KeyInfo) =
    key.State

let keys = getKeys walletname
let intSegwit = filterByKeyPath keys "84'/1'/0'/1" |> List.map stateToHeat
let extSegwit = filterByKeyPath keys "84'/1'/0'/0" |> List.map stateToHeat
let intTaproot= filterByKeyPath keys "86'/1'/0'/1" |> List.map stateToHeat
let extTaproot= filterByKeyPath keys "86'/1'/0'/0" |> List.map stateToHeat

let matrix = [ intSegwit; extSegwit; intTaproot; extTaproot ]
 
let rownames = ["segwit Int"; "segwit Ext"; "taproot Int"; "taproot ext"]
let colnames = []

let heat =
    Chart.Heatmap(
        matrix,
        colnames,
        rownames,
        UseDefaults = false
    )

heat |> Chart.show
