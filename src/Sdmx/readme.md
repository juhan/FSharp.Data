# SdmxProvider

```fsharp
#r @"../../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
#load @"../../../../packages/test/FSharp.Charting/FSharp.Charting.fsx"
open FSharp.Data
open FSharp.Charting

// WorldBank Provider For Comparision
let data = WorldBankData.GetDataContext()
let wbData = data.Countries.``United Kingdom``.Indicators.``Gross capital formation (% of GDP)``

// SDMX version
type WB = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">

let a = WB.``World Development Indicators``.``Frequency code list``.Annual
let b = WB.``World Development Indicators``.``Reference area code list``.``United Kingdom``
let c = WB.``World Development Indicators``.``Series code list``.``Gross capital formation (% of GDP)``

let wdiDataflow = WB.``World Development Indicators``()
let sdmxData = wdiDataflow.FetchData(a, b, c).Data

let wch = wbData |> Chart.Line
let sch = sdmxData |> Chart.Line
Chart.Combine( [Chart.Line(sdmxData); Chart.Line(wbData)] )

```

## Setup

    git clone https://github.com/demonno/FSharp.Data
    git checkout sdxm-types

## Build

    sh build.sh Build

## Test

Restart existig F# Interactive shell after each build to see new results.