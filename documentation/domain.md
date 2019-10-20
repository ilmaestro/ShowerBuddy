# domain modeling

## Calculation Model

- keep track of total shower time
- model inputs: flow rate (gpm)
- output: water usage (g)

```fsharp
[<Measure>] type gpm
[<Measure>] type g

type ModelState = {
    WaterUsage  : float<g>
    FlowRate    : float<gpm>
    }

type ModelOutput = ModelState -> float<g>

```

## Audio Model

- Take input from audio (microphone)
- Do analysis
- Translate to shower on or shower off events

```fsharp
type ShowerEvent =
    | TurnedOn
    | TurnedOff

(* Analysis *)
type AudioInput = exn
type ShowerAnalyzer = AudioInput -> ShowerEvent seq

```

## Display Model

- visualize the audio stream

```fsharp
type DisplayOutput = exn
type AudioDisplay = AudioInput -> DisplayOutput

```