// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Akkling
open Serilog
open Push

[<EntryPoint>]
let main _ =
    let createGuid (i: byte) =
      [|
        for _ in 0uy..14uy do 0uy
        i
      |] |> Guid

    let alice1 = { AccountId = createGuid 1uy; FcmToken = "alice_1" }
    let alice2 = { AccountId = alice1.AccountId; FcmToken = "alice_2" }
    let bob = { AccountId = createGuid 2uy; FcmToken = "bob_1" }
    Log.Logger <- (new LoggerConfiguration())
                    .WriteTo
                    .Console()
                    .MinimumLevel
                    .Information()
                    .CreateLogger();

    let config = Configuration.parse "
akka {
  stdout-loglevel = DEBUG
  loglevel = DEBUG
}"

    let system = System.create "notification-system" config
    let writer = spawn system "console-writer" (props Push.writer)
    let server = spawn system "notification-server" (props <| Push.Server.handle writer)

    server <! AddDevice alice1
    server <! AddDevice alice2
    server <! Tell { AccountId = alice1.AccountId; Text = "hey" }
    server <! RemoveDevice alice2
    server <! RemoveDevice alice1
    server <! Tell { AccountId = alice1.AccountId; Text = "how are you?" }

    printfn "Press Enter to exit"
    let _ = Console.ReadLine()
    printfn "Done!"
    0