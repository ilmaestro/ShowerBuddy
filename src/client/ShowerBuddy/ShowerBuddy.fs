// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace ShowerBuddy

open System.Diagnostics
open Fabulous
open Fabulous.XamarinForms
open Fabulous.XamarinForms.LiveUpdate
open Xamarin.Forms
open ShowerBuddy.Interfaces
open ShowerBuddy.Domain
open System.Collections.Generic
open System
open System.Timers

module App = 

    type Model = 
      { Volume          : int
        CurrentSample   : SampleVolume
        SamplerOn       : bool
        TotalShowerTime : TimeSpan
        ErrorMsg        : string option }

    type Msg = 
        | Reset
        | TimerTick of TimeSpan
        | SamplerToggled of bool
        | ReceiveSample of SampleVolume
        | ShowError of string

    let initModel = { Volume = -100; CurrentSample = SampleVolume -100.;  SamplerOn = false; TotalShowerTime = TimeSpan.FromMilliseconds(0.); ErrorMsg = None }

    let init () = initModel, Cmd.none
    let mutable isSampling = false

    let startSamplingCmd (audioSampler: IAudioSampler) =
        Cmd.ofAsyncMsgOption (async {
            if not isSampling then
                isSampling <- true
                do! Async.SwitchToThreadPool()
                let! result = audioSampler.Start()
                let msg =
                    match result with
                    | Ok () -> None
                    | Error message -> Some (ShowError message)

                return msg
            else
                return None
        })

    let stopSamplingCmd (audioSampler: IAudioSampler) =
        Cmd.ofAsyncMsgOption (async { 
            let msg =
                match audioSampler.Stop() with
                | Ok () -> None
                | Error message -> Some (ShowError message)
            isSampling <- false
            return msg
        })

    let update (audioSampler: IAudioSampler) msg model =
        match msg with
        | Reset -> init ()
        | TimerTick tick ->
            if model.Volume > -46 then
                {model with TotalShowerTime = model.TotalShowerTime + tick}, Cmd.none
            else model, Cmd.none
        | SamplerToggled on ->
            let cmd = 
                if on then (startSamplingCmd audioSampler)
                else (stopSamplingCmd audioSampler)
            { model with SamplerOn = on }, cmd    
        | ShowError msg -> { model with ErrorMsg = Some msg }, Cmd.none
        | ReceiveSample (SampleVolume sample) ->
            let dB = int (20. * Math.Log10(sample))
            { model with CurrentSample = (SampleVolume sample); Volume = dB }, Cmd.none

    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.Center,
            children = [
                yield View.Label(text = "Shower Time", horizontalOptions = LayoutOptions.Center)
                yield View.Label(text = (sprintf "%i:%i.%i" model.TotalShowerTime.Minutes model.TotalShowerTime.Seconds model.TotalShowerTime.Milliseconds), horizontalOptions = LayoutOptions.StartAndExpand, horizontalTextAlignment = TextAlignment.Center)
                // Shower Alert
                if model.Volume > -46 then yield View.Label(text = "SHOWER IS ON!", horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center, textColor = Color.Red)
                // error message
                match model.ErrorMsg with Some msg -> yield View.Label(text = sprintf "ERROR: %s" msg, horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center) | _ -> ()

                // Labels
                yield View.Label(text = "Audio Volume", horizontalOptions = LayoutOptions.Center)
                yield View.Label(text = sprintf "%d" model.Volume, horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center)
                yield View.Label(text = sprintf "%A" model.CurrentSample, horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center)
                yield View.Switch(isToggled = model.SamplerOn, toggled = (fun on -> dispatch (SamplerToggled on.Value)), horizontalOptions = LayoutOptions.Center)
                //yield View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset), canExecute = (model <> initModel))
            ]))

    let subscription (audioSampler: IAudioSampler) _ =
        Cmd.ofSub (fun dispatch ->
            do
                let bufferSize = 100
                let buffer = Array.zeroCreate<float> bufferSize
                let mutable bufferIndex = 0
                audioSampler.OnSampleEvent.Publish
                    .Subscribe(fun (SampleVolume sample) ->
                        if bufferIndex < bufferSize then
                            buffer.[bufferIndex] <- sample
                        else
                            bufferIndex <- 0
                            let avg = Array.average buffer
                            dispatch (ReceiveSample (SampleVolume avg))
                            buffer.[bufferIndex] <- sample
                        
                        bufferIndex <- bufferIndex + 1
                        )
                    |> ignore
            do
                let tick = TimeSpan.FromMilliseconds(100.)
                let timer = new Timer(tick.TotalMilliseconds)
                timer.Elapsed.Subscribe(fun _ -> dispatch (TimerTick tick)) |> ignore
                timer.Enabled <- true
                timer.Start()
            )

    let program audioSampler = 
        let _update = update audioSampler
        Program.mkProgram init _update view

type App (audioSampler: IAudioSampler) as app = 
    inherit Application ()

    let runner = 
        App.program audioSampler
        |> Program.withSubscription (App.subscription audioSampler)
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/tools.html#live-update for further  instructions.
    //
    //do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/models.html#saving-application-state for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


