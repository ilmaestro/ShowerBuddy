namespace ShowerBuddy.Interfaces
open ShowerBuddy.Domain

type IAudioSampler =
    abstract member OnSampleEvent   : Event<VolumeSample> with get
    abstract member Start           : unit -> Async<Result<unit, string>>
    abstract member Stop            : unit -> Result<unit, string>
    abstract member Release         : unit -> Result<unit, string>
