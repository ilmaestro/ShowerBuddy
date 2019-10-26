namespace ShowerBuddy.iOS

open ShowerBuddy.Interfaces
open ShowerBuddy.Domain

type AudioService() =
    let onSample = Event<VolumeSample>()

    interface IAudioSampler with
        member this.OnSampleEvent with get () = onSample
        member this.Start() = async {
            do! Async.Sleep 100
            return Ok ()
            }
        member this.Stop() = Ok ()
        member this.Release() = Ok ()
