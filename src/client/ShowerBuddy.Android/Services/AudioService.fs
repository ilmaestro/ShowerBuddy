namespace ShowerBuddy.Android
open ShowerBuddy.Interfaces
open Android.Media
open System
open System.Threading
open Plugin.Permissions


// TODO: https://stackoverflow.com/questions/49653692/startrecording-called-on-an-uninitialized-audiorecord
type AudioService() =


    member private this.CheckPermissions () = async {
        let! status = CrossPermissions.Current.CheckPermissionStatusAsync<MicrophonePermission>() |> Async.AwaitTask
        let mutable result = true
        if not <| (status = Plugin.Permissions.Abstractions.PermissionStatus.Granted) then
            let! request = CrossPermissions.Current.RequestPermissionAsync<MicrophonePermission>() |> Async.AwaitTask
            result <- request = Plugin.Permissions.Abstractions.PermissionStatus.Granted
        return result
        }

    interface IAudioService with
        member this.StartAnalyzer bufferLength analyzer cancellationToken = async {
            let! hasPermission = this.CheckPermissions()
            if not hasPermission then failwith "Failed to get permission to microphone"

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