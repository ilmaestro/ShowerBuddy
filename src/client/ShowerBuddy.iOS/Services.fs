namespace ShowerBuddy.iOS

open ShowerBuddy.Interfaces

type AudioService() =

    interface IAudioService with
        member this.StartAnalyzer bufferLength analyzer cancellationToken = async {
            do! Async.Sleep 1000
            return Ok ()
            }