namespace ShowerBuddy.iOS

open ShowerBuddy.Interfaces

type AudioService() =
    let onSample = Event<byte[]>()

    interface IAudioSampler with
        member this.OnSampleEvent with get () = onSample
        member this.Start() = async {
            do! Async.Sleep 100
            return Ok ()
            }
        member this.Stop() = Ok ()
        member this.Release() = Ok ()

    interface IAudioService with
        member this.StartAnalyzer bufferLength analyzer cancellationToken = async {
            do! Async.Sleep 100
            return Ok ()
            }