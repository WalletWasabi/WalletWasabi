#r "System.Security.Cryptography.dll"
open System
open System.Security.Cryptography

let hex (data: byte[]) = data |> Convert.ToHexString |> fun x -> x.ToLowerInvariant()

let ecdsa = ECDsa.Create ECCurve.NamedCurves.nistP256
printfn $"secretKey: %s{hex (ecdsa.ExportECPrivateKey())}"
printfn $"publicKey: %s{hex (ecdsa.ExportSubjectPublicKeyInfo())}"
