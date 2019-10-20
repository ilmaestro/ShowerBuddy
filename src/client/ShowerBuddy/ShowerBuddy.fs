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

module App = 
    type Model = 
      { Count       : int
        Step        : int
        TimerOn     : bool
        ErrorMsg    : string option }

    type Msg = 
        | Increment 
        | Decrement 
        | Reset
        | SetStep of int
        | TimerToggled of bool
        | TimedTick
        | AverageDbReading of float
        | ShowError of string

    let initModel = { Count = 0; Step = 1; TimerOn=false; ErrorMsg = None }

    let init () = initModel, Cmd.none

    let timerCmd (audioService: IAudioService) =
        async {
            let cancellationSource = new System.Threading.CancellationTokenSource(2000)
            let samples = new List<int16>()
            let collector buffer =
                samples.AddRange(Array.toSeq buffer)
            // start collecting samples
            let! output = audioService.StartAnalyzer 100_000 collector (cancellationSource.Token)
            match output with
            | Ok () ->
                let reading = samples.ToArray() |> AudioAnalyzer.averageDecibel
                return AverageDbReading reading
            | Error msg ->
                return ShowError msg
            }
        |> Cmd.ofAsyncMsg


    let update (audioService: IAudioService) msg model =
        match msg with
        | Increment -> { model with Count = model.Count + model.Step }, Cmd.none
        | Decrement -> { model with Count = model.Count - model.Step }, Cmd.none
        | Reset -> init ()
        | SetStep n -> { model with Step = n }, Cmd.none
        | TimerToggled on -> { model with TimerOn = on }, (if on then (timerCmd audioService) else Cmd.none)
        | TimedTick -> 
            if model.TimerOn then 
                { model with Count = model.Count + model.Step }, (timerCmd audioService)
            else 
                model, Cmd.none
        | ShowError msg -> { model with ErrorMsg = Some msg }, Cmd.none
        | AverageDbReading reading -> { model with Count = (int reading) }, Cmd.none

    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.Center,
            children = [
                match model.ErrorMsg with Some msg -> yield View.Label(text = sprintf "ERROR: %s" msg, horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center) | _ -> ()
                yield View.Label(text = sprintf "%d" model.Count, horizontalOptions = LayoutOptions.Center, widthRequest=200.0, horizontalTextAlignment=TextAlignment.Center)
                //View.Button(text = "Increment", command = (fun () -> dispatch Increment), horizontalOptions = LayoutOptions.Center)
                //View.Button(text = "Decrement", command = (fun () -> dispatch Decrement), horizontalOptions = LayoutOptions.Center)
                yield View.Label(text = "Audio", horizontalOptions = LayoutOptions.Center)
                yield View.Switch(isToggled = model.TimerOn, toggled = (fun on -> dispatch (TimerToggled on.Value)), horizontalOptions = LayoutOptions.Center)
                //View.Slider(minimumMaximum = (0.0, 10.0), value = double model.Step, valueChanged = (fun args -> dispatch (SetStep (int (args.NewValue + 0.5)))), horizontalOptions = LayoutOptions.FillAndExpand)
                //View.Label(text = sprintf "Step size: %d" model.Step, horizontalOptions = LayoutOptions.Center) 
                yield View.Button(text = "Reset", horizontalOptions = LayoutOptions.Center, command = (fun () -> dispatch Reset), canExecute = (model <> initModel))
            ]))

    // Note, this declaration is needed if you enable LiveUpdate
    let program audioService = 
        let _update = update audioService
        Program.mkProgram init _update view

type App (audioService: IAudioService) as app = 
    inherit Application ()

    let runner = 
        App.program audioService
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


