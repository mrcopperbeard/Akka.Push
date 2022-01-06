module Push

open System

type AccountNotification = {
    AccountId: Guid
    Text: string
}

type UserDevice = {
    AccountId: Guid
    FcmToken: string
}

type DeviceNotification = {
    FcmToken: string
    Text: string
}

type ServerMessage =
| AddDevice of UserDevice
| RemoveDevice of UserDevice
| RemoveAccount of Guid
| Tell of AccountNotification

open Akkling

module Notifier =
    type Message =
    | Tell of string
    | AddDevice of string
    | RemoveDevice of UserDevice

    let handle (writer: IActorRef<DeviceNotification>) (ctx: Actor<Message>) =
        let rec loop devices = actor {
            let! message = ctx.Receive()
            match message with
            | Tell text ->
                devices |> Set.iter (fun device -> writer <! { Text = text; FcmToken = device })
            | AddDevice device -> return! devices |> Set.add device |> loop
            | RemoveDevice device ->
                let remained = devices |> Set.remove device.FcmToken
                if remained |> Set.isEmpty then
                    ctx.Sender() <! RemoveAccount device.AccountId
                    return Stop
                else return! loop remained
        }

        loop Set.empty

module Server =
    let handle (writer: IActorRef<DeviceNotification>) (ctx: Actor<ServerMessage>) =
        let createNotifier = Notifier.handle writer
        let rec loop notifiers = actor {
            let! message = ctx.Receive()
            match message with
            | AddDevice device ->
                let current, updated = 
                    notifiers
                    |> Map.tryFind device.AccountId
                    |> function
                    | Some notifier -> notifier, notifiers
                    | None ->
                        let notifier = spawn ctx $"notifier-{device.AccountId}" <| (props createNotifier)
                        notifier, notifiers |> Map.add device.AccountId notifier
                current <! Notifier.AddDevice device.FcmToken
                return! loop updated
            | RemoveDevice device ->
                notifiers
                |> Map.tryFind device.AccountId
                |> function
                | None -> ()
                | Some notifier -> notifier <! Notifier.RemoveDevice device

            | Tell notification ->
                notifiers
                |> Map.tryFind notification.AccountId
                |> function
                | Some notifier -> notifier <! Notifier.Tell notification.Text
                | None -> logDebug ctx $"Tell: No receivers for account {notification.AccountId} were found"
            | RemoveAccount accountId ->
                let remained = notifiers |> Map.remove accountId
                return! loop remained
        }
        let notifiers = Map.empty<Guid, IActorRef<Notifier.Message>>
        loop notifiers

let writer (ctx: Actor<DeviceNotification>) =
    let rec loop () = actor {
        let! message = ctx.Receive()

        logInfo ctx <| sprintf "%s <-- %s" message.FcmToken message.Text
    }

    loop ()

