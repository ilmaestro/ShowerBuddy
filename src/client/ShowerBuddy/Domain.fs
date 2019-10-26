namespace ShowerBuddy.Domain

open System
type VolumeSample = VolumeSample of float

type SamplingState =
    | Off
    | SampleNoise
    | SampleVolume

