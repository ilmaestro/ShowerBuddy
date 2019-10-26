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
        Noise           : int
        CurrentSample   : VolumeSample
        SamplingState   : SamplingState
        CountdownTime   : TimeSpan
        TotalWaterTime  : TimeSpan
        TotalShowerTime : TimeSpan
        ErrorMsg        : string option }

    with 
        member this.ShowerOn = this.Volume - this.Noise >= 3 // if the volume is more than 3dB above the noise
        member this.WaterUsage = this.TotalWaterTime.TotalMinutes * 2.5 // assuming 2.5 gallons per minute

    type Msg = 
        | Reset
        | TimerTick of TimeSpan
        | Enable of bool
        | ReceiveSample of VolumeSample
        | ShowError of string

    let initModel = {
        Volume = -100
        Noise = -100
        CurrentSample = VolumeSample -100.
        SamplingState = Off
        CountdownTime = TimeSpan.FromMilliseconds(0.)
        TotalWaterTime = TimeSpan.FromMilliseconds(0.)
        TotalShowerTime = TimeSpan.FromMilliseconds(0.)
        ErrorMsg = None }

    let init () = initModel, Cmd.none

    let startSamplingCmd (audioSampler: IAudioSampler) =
        Cmd.ofAsyncMsgOption (async {
            do! Async.SwitchToThreadPool()
            let! result = audioSampler.Start()
            let msg =
                match result with
                | Ok () -> None
                | Error message -> Some (ShowError message)

            return msg
        })

    let stopSamplingCmd (audioSampler: IAudioSampler) =
        Cmd.ofAsyncMsgOption (async { 
            let msg =
                match audioSampler.Stop() with
                | Ok () -> None
                | Error message -> Some (ShowError message)
            return msg
        })

    let update (audioSampler: IAudioSampler) msg (model : Model)=
        match msg with
        | Reset -> init ()
        | TimerTick tick ->
            match model.SamplingState, model.ShowerOn with
            | SampleVolume, true ->
                {model with TotalShowerTime = model.TotalShowerTime + tick; TotalWaterTime = model.TotalWaterTime + tick}, Cmd.none
            | SampleVolume, false ->
                // only count shower time after its been incremented (by shower turning on)
                if model.TotalShowerTime.TotalSeconds > 0.
                then {model with TotalShowerTime = model.TotalShowerTime + tick}, Cmd.none
                else model, Cmd.none
            | SampleNoise, _ ->
                {model with CountdownTime = model.CountdownTime - tick}, Cmd.none
            | _, _ -> model, Cmd.none
        | Enable true ->
            { initModel with  // use init model to reset
                SamplingState = SampleNoise;
                CountdownTime = TimeSpan.FromSeconds(10.);
                }, (startSamplingCmd audioSampler)
        | Enable false ->
            { model with SamplingState = Off; CountdownTime = TimeSpan.FromSeconds(0.) }, (stopSamplingCmd audioSampler)
        | ShowError msg -> { model with ErrorMsg = Some msg }, Cmd.none
        | ReceiveSample (VolumeSample sample) ->
            let dB = int (20. * Math.Log10(sample))
            match model.SamplingState with
            | SampleNoise ->
                let nextState = if model.CountdownTime.TotalSeconds > 0. then SampleNoise else SampleVolume
                let averageNoise = (dB + model.Noise) / 2
                { model with SamplingState = nextState; CurrentSample = (VolumeSample sample); Noise = averageNoise }, Cmd.none
            | SampleVolume ->
                let averageVolume = (dB + model.Volume) / 2
                { model with CurrentSample = (VolumeSample sample); Volume = averageVolume }, Cmd.none
            | Off ->
                model, Cmd.none

    let displayTimespan prefix (timespan : TimeSpan) =
        sprintf "%s: %s" prefix (timespan.ToString("mm\:ss\.ff"))

    let displayGallons prefix (model : Model) =
        sprintf "%s: %s gal" prefix (model.WaterUsage.ToString("#,#00.00"))

    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.StartAndExpand,
            children = [
                yield View.Label(text = displayTimespan "Shower Time" model.TotalShowerTime, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment = TextAlignment.Start)
                yield View.Label(text = displayTimespan "Water Time" model.TotalWaterTime, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment = TextAlignment.Start)
                yield View.Label(text = displayGallons "Water Usage" model, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment = TextAlignment.Start)
                // Shower Alert
                if model.ShowerOn then yield View.Label(text = "SHOWER IS ON!", horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Start, textColor = Color.Red)
                // error message
                match model.ErrorMsg with Some msg -> yield View.Label(text = sprintf "ERROR: %s" msg, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Start) | _ -> ()

                // Labels
                yield View.Label(text = sprintf "Volume: %i" model.Volume, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Start)
                yield View.Label(text = sprintf "Noise: %i" model.Noise, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Start)
                //yield View.Label(text = sprintf "%A" model.CurrentSample, horizontalOptions = LayoutOptions.StartAndExpand, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Start)
                yield View.Switch(isToggled = (model.SamplingState <> Off), toggled = (fun on -> dispatch (Enable on.Value)), horizontalOptions = LayoutOptions.Start)
                //yield View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset), canExecute = (model <> initModel))
            ]))

    let subscription (audioSampler: IAudioSampler) _ =
        Cmd.ofSub (fun dispatch ->
            do
                // buffer the buffer!
                let bufferSize = 50
                let buffer = Array.zeroCreate<float> bufferSize
                let mutable bufferIndex = 0
                audioSampler.OnSampleEvent.Publish
                    .Subscribe(fun (VolumeSample sample) ->
                        if bufferIndex < bufferSize then
                            buffer.[bufferIndex] <- sample
                        else
                            bufferIndex <- 0
                            let avg = Array.average buffer
                            dispatch (ReceiveSample (VolumeSample avg))
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


