#r "nuget:FSharp.Data"

open System
open System.IO

module Config =
  open FSharp.Data


  type Config = JsonProvider<"""{
    "JsonRpcServerEnabled": true,
    "JsonRpcUser": "",
    "JsonRpcPassword": "",
    "JsonRpcServerPrefixes": [
      "http://127.0.0.1:37128/"
    ]
  }""">


  let getConfig () = Config.Load(
    Path.Combine (
      Environment.ExpandEnvironmentVariables ("%HOME%/.walletwasabi/client/"),
      "Config.json"))

module Rpc =
  open System.Net.Http

  let config =  Config.getConfig ()
  let http = new HttpClient()
  let makeRpcRequestAsync (walletName: string) (methodName: string) = async {
    let content = new StringContent ($"{{\"jsonrpc\":\"2.0\", \"id\":\"id\", \"method\":\"selectwallet\", \"params\":[\"{walletName}\"]}}")
    let! response = http.PostAsync(config.JsonRpcServerPrefixes[0], content) |> Async.AwaitTask

    let content = new StringContent ($"{{\"jsonrpc\":\"2.0\", \"id\":\"id\", \"method\":\"{methodName}\"}}")
    let! response = http.PostAsync(config.JsonRpcServerPrefixes[0], content) |> Async.AwaitTask
    let! jsonResult = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    return jsonResult
  } 


  open FSharp.Data
  type ListCoinsRpcResponse = JsonProvider<"""{
    "result": [
      {"txid":"73af1dd","index":0,"amount":4300000000000,"anonymitySet":"a1.0","confirmed":true,"confirmations":116,"keyPath":"84/0","address":"tb1q","spentBy":"2d7c3f"}
    ]
  }""">

  type ListKeysRpcResponse = JsonProvider<"""{
    "result": [
      {"fullKeyPath":"xxxx","keyState":0}
    ]
  }""">

  let getListOfKeys (walletName: string) =
    makeRpcRequestAsync walletName "listkeys"
    |> Async.RunSynchronously 
    |> ListKeysRpcResponse.Parse
    |> fun x -> x.Result

  let getListOfCoins (walletName: string) =
    makeRpcRequestAsync walletName "listcoins"
    |> Async.RunSynchronously 
    |> ListCoinsRpcResponse.Parse
    |> fun x -> x.Result
    