#r @"../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open FSharp.Data
type SD = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">

SD.SDG.SDGFREQ

sdg.


let c = SD.GetDataContext()
let dataflows = c.Dataflows

dataflows.``World Development Indicators``

let dfC = SD.GetDataflowContext()

let dataflows = df.Dataflows


dataflows.``World Development Indicators``
// let wdi = dataflows.``World Development Indicators``

let d1 = wdi.FREQ.Annual
let d2 = wdi.REF_AREA.Estonia
let d3 = wdi.SRIES.AG_AGR_TRAC_NO

let data = SD.GetDataContext([d1;d2;d3])



let wdi = wb.``World Development Indicators``

let a = sdg.

wb.Dataflows.``World Development Indicators``.FREQ.Annual.


// SdmxDataProvider<"World Development Indicators", Asynchronous=true>
// let wb = SdmxData.GetDataContext()

// wb.WDI.
// wb.[Dataflow].[Dimention1].[Dimention2]


// wb.Countries.Togo.CapitalCity
