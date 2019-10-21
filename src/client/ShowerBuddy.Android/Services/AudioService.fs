namespace ShowerBuddy.Android
open ShowerBuddy.Domain
open ShowerBuddy.Interfaces
open Android.Media
open System
open System.Threading
open Plugin.Permissions

// TODO: https://github.com/tyorikan/voice-recording-visualizer/blob/master/visualizer/src/main/java/com/tyorikan/voicerecordingvisualizer/RecordingSampler.java
type AudioService(bufferLength, samplingRate) =

    let mutable isSampling = false
    let mutable sampleBuffer = Array.zeroCreate<byte> bufferLength
    let sampler = new AudioRecord(
                    // source
                    AudioSource.Mic,
                    // frequency
                    samplingRate,
                    // channels
                    ChannelIn.Mono,
                    //encoding
                    Encoding.Pcm16bit,
                    // buffer size
                    bufferLength
                    )

    let onSample = Event<SampleVolume>()

    member private this.CheckPermissions () = async {
        let! status = CrossPermissions.Current.CheckPermissionStatusAsync<MicrophonePermission>() |> Async.AwaitTask
        let mutable result = true
        if not <| (status = Plugin.Permissions.Abstractions.PermissionStatus.Granted) then
            let! request = CrossPermissions.Current.RequestPermissionAsync<MicrophonePermission>() |> Async.AwaitTask
            result <- request = Plugin.Permissions.Abstractions.PermissionStatus.Granted
        return result
        }

    interface IAudioSampler with
        member this.OnSampleEvent with get () = onSample

        member this.Start() = async {
            let! hasPermission = this.CheckPermissions()
            if not hasPermission then
                return (Error "Failed to get permission to microphone")
            else
                if not isSampling then
                    isSampling <- true
                    sampler.StartRecording()

                    let rec recordLoop () = async {
                        do! Async.Sleep 100 // delay
                        try
                            if isSampling then
                                do! sampler.ReadAsync(sampleBuffer, 0, bufferLength) |> Async.AwaitTask |> Async.Ignore

                                // calculate average volume
                                let volume = sampleBuffer |> Seq.averageBy (fun v -> Math.Abs(float v / 32768.))

                                onSample.Trigger(SampleVolume volume)

                                return! recordLoop ()
                            else
                                return Ok()
                        with
                        | ex ->
                            return Error (ex.Message)
                        }
       
                    return! recordLoop ()
                else
                    return (Error "already recording")
            }
    
        member this.Stop() =
            if isSampling then
                sampler.Stop()
                isSampling <- false
                Ok ()
            else
                Error "not recording"
    
        member this.Release() =
            if isSampling then sampler.Stop()
            sampler.Release()
            Ok ()