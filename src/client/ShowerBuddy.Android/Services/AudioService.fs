namespace ShowerBuddy.Android
open ShowerBuddy.Domain
open ShowerBuddy.Interfaces
open Android.Media
open System
open System.Threading
open Plugin.Permissions

// TODO: https://github.com/tyorikan/voice-recording-visualizer/blob/master/visualizer/src/main/java/com/tyorikan/voicerecordingvisualizer/RecordingSampler.java
type MicrophoneSampler() =

    let samplingRate = 44100
    let conversionFactor = 32768.0 // based on 16bit
    let bufferSize = AudioRecord.GetMinBufferSize(samplingRate, ChannelIn.Mono, Encoding.Pcm16bit)
    let mutable sampleBuffer = Array.zeroCreate<byte> bufferSize
    let sampler = new AudioRecord(AudioSource.Default, samplingRate, ChannelIn.Mono, Encoding.Pcm16bit, bufferSize)
    let onSample = Event<VolumeSample>()
    let mutable isSampling = false

    // https://stackoverflow.com/questions/7955041/voice-detection-in-android-application/7976877#7976877
    let voiceAnalyzer bytesRead (buffer : byte[]) =
        let samples = seq { 
            for i in 0 .. bytesRead - 1 .. 2 ->
                int16((buffer.[i + 1] <<< 8) ||| buffer.[i])
            }
        samples
        |> Seq.map (fun sample -> Math.Abs(float sample / conversionFactor))
        |> Seq.max
        |> VolumeSample

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
                        //do! Async.Sleep 100 // delay
                        try
                            if isSampling && sampler.State = State.Initialized then
                                let! numberOfBytesRead = sampler.ReadAsync(sampleBuffer, 0, bufferSize) |> Async.AwaitTask
                                if numberOfBytesRead > 0 then onSample.Trigger(voiceAnalyzer numberOfBytesRead sampleBuffer)
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
                isSampling <- false
                sampler.Stop()
                Ok ()
            else
                Error "not recording"
    
        member this.Release() =
            if isSampling then sampler.Stop()
            sampler.Release()
            Ok ()