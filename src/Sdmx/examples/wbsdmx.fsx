#r @"../../../bin/lib/net45/FSharp.Data.dll"
// #r "System.Xml.Linq.dll"

open FSharp.Data
type SD = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">
let dd = SD.GetDataContext()
dd.Dataflows.SDG
let wb = SD.GetDataflowContext().``World Development Indicators``
let wdi = wb.``World Development Indicators``

let a = sdg.

wb.Dataflows.``World Development Indicators``.FREQ.Annual.


// SdmxDataProvider<"World Development Indicators", Asynchronous=true>
// let wb = SdmxData.GetDataContext()

// wb.WDI.
// wb.[Dataflow].[Dimention1].[Dimention2]


// wb.Countries.Togo.CapitalCity
