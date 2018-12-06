# SdmxProvider

```fsharp
#r @"../../../bin/lib/net45/FSharp.Data.dll"

open FSharp.Data
type SD = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">

let wb = SD.GetDataContext()

wb.Dataflows.SDG.Dimensions.FREQ

```

## Setup

    git clone https://github.com/demonno/FSharp.Data
    git checkout sdxm-experiments

## Build

    sh build.sh Build

## Test

Restart existig F# Interactive shell after each build to see new results.