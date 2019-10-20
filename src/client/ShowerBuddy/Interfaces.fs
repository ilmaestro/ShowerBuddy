namespace ShowerBuddy.Interfaces
open ShowerBuddy.Domain
open System.Threading

type IAudioService =
    abstract member StartAnalyzer: BufferLength -> AudioBufferAnalyzer -> CancellationToken -> Async<Result<unit, string>>