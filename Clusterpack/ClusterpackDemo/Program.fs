﻿// Learn more about F# at http://fsharp.org

open System
open System.Threading
open Clusterpack
open Clusterpack.Grpc
open FSharp.Control.Tasks.Builders
open FSharp.Core

let start nodeId endpoint =
    let transport = new GrpcTransport(nodeId, endpoint)
    new Node(transport)
    
let run (cancel: CancellationToken) = unitVtask {    
    use a = start 1u "127.0.0.1:10001"
    use b = start 2u "127.0.0.1:10002"
    
    // connect two nodes together - returns nodeId of node B
    let! _ = a.Connect("127.0.0.1:10002", cancel)
    
    // create a channel and wrap it using node A
    let (writer, reader) = Channel.boundedMpsc 100
    let local = a.Wrap(fun address -> writer)
    
    // tries to create a remote proxy using given address (address is serializable tuple (u32,u32)) 
    match b.Proxy(local.Address) with
    | None -> printfn "Couldn't target '%O' from node '%O'" local.Address b.Manifest.NodeId
    | Some remote ->
        // send message to channel "living" on node A via remote proxy from node B
        do! remote.WriteAsync("Hello from remote!")
    
    // receive message send remotely
    let! message = reader.ReadAsync(cancel)
    printfn "received %s" message
    
    //TODO: graceful shutdown
    do! a.DisposeAsync()
    do! b.DisposeAsync()
}

[<EntryPoint>]
let main argv =
    use cancel = new CancellationTokenSource(10_000)
    run(cancel.Token).GetAwaiter().GetResult()
    0 // return an integer exit code