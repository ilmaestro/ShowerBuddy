namespace ShowerBuddy.Domain

open System

type AudioBuffer = int16[]
type BufferLength = int32
type AudioBufferAnalyzer = AudioBuffer -> unit
type AudioAnalyzer<'T> = AudioBuffer -> 'T

module AudioAnalyzer =
    let dB (amplitude : int16) = Math.Log10(Math.Abs((float)amplitude))

    let averageDecibel: AudioAnalyzer<float> = Array.averageBy dB

