#r @"../../../bin/lib/net45/FSharp.Data.dll"
// #r "System.Xml.Linq.dll"

open FSharp.Data
type SD = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">

let wb = SD.GetDataContext()

wb.Dataflows.``World Development Indicators``.FREQ.Annual.


// SdmxDataProvider<"World Development Indicators", Asynchronous=true>
// let wb = SdmxData.GetDataContext()

// wb.WDI.
// wb.[Dataflow].[Dimention1].[Dimention2]


// wb.Countries.Togo.CapitalCity
