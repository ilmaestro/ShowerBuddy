namespace ShowerBuddy.Android
open ShowerBuddy.Interfaces
open Android.Media
open System
open System.Threading


// TODO: https://stackoverflow.com/questions/49653692/startrecording-called-on-an-uninitialized-audiorecord
type AudioService() =

    interface IAudioService with
        member this.StartAnalyzer bufferLength analyzer cancellationToken = async {
            let mutable audioBuffer = Array.zeroCreate<int16> bufferLength
            let recorder = new AudioRecord(
                                // source
                                AudioSource.Mic,
                                // frequency
                                11025,
                                // channels
                                ChannelIn.Mono,
                                //encoding
                                Encoding.Pcm16bit,
                                // buffer size
                                bufferLength
                                )

            recorder.StartRecording()
            let dispose () = recorder.Release()

            let rec recordLoop () = async {
                try
                    cancellationToken.ThrowIfCancellationRequested()
                    do! recorder.ReadAsync(audioBuffer, 0, bufferLength) |> Async.AwaitTask |> Async.Ignore
                    
                    analyzer audioBuffer

                    return! recordLoop ()
                with
                | :? OperationCanceledException ->
                    dispose ()
                    return Ok ()
                | _ as ex ->
                    dispose ()
                    return Error (ex.Message)
                }
       
            return! recordLoop ()
            }