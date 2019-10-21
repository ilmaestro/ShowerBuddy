# things i dont know

- how to stream audio from microphone
- how to display audio stream
- how to analyze audio stream
- how to trigger events from analysis

## resources
- <https://docs.microsoft.com/en-us/xamarin/android/app-fundamentals/android-audio#initializing-and-recording-1>
- https://github.com/DieselMeister/perry-rhodan-audiobook-player/blob/master/PerryRhodan.AudiobookPlayer

## how to stream audio (android)

```csharp
void RecordAudio()
{
  byte[] audioBuffer = new byte[100000];
  var audRecorder = new AudioRecord(
    // Hardware source of recording.
    AudioSource.Mic,
    // Frequency
    11025,
    // Mono or stereo
    ChannelIn.Mono,
    // Audio encoding
    Android.Media.Encoding.Pcm16bit,
    // Length of the audio clip.
    audioBuffer.Length
  );
  audRecorder.StartRecording();
  while (true) {
    try
    {
      // Keep reading the buffer while there is audio input.
      audRecorder.Read(audioBuffer, 0, audioBuffer.Length);
      // Write out the audio file.
    } catch (Exception ex) {
      Console.Out.WriteLine(ex.Message);
      break;
    }
  }

  // Calling the Stop method terminates the recording:
  audRecorder.Stop();
  // When the AudioRecord object is no longer needed, calling its Release method releases all resources associated with it:
  audRecorder.Release();
}
```

## how to stream audio (ios)

??

## how to wire up events in fabulous

- [Triggering Commands from External Events](https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/update.html)
- program should subscribe to Sampler service

```fsharp
let subscribeToPushEvent dispatch = 
     ...
     call dispatch in some closure
     ...

let runner = 
    Program.mkSimple App.init App.update App.view
    |> Program.withSubscription (fun _ -> Cmd.ofSub subscribeToPushEvent)
    |> Program.runWithDynamicView app
```