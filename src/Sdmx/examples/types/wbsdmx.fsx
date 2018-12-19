#r @"../../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open FSharp.Data
open System.IO
open System.Xml
open System.Xml.Linq
open System

// type ECB = SdmxDataProvider<"https://sdw-wsrest.ecb.europa.eu/service"> //"/dataflow/all/all/latest/"
// type BANKNOTES = ECB.``Banknotes statistics``

type WB = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">
type WDI = WB.``World Development Indicators``

WDI.``Frequency code list``


// WB.``World Development Indicators``.WDIFREQ.Annual

// type WDI = WB.``World Development Indicators``
// WDI.
// let area = WDI.``Reference area code list``.Georgia
// let series = WDI.``Series code list``.``Demand for family planning satisfied by modern methods (% of married women with demand for family planning)``


// let freq = SD.SDG.SDGFREQ.Annual
// let ref = SD.SDG.SDGFREQ_AREA.GEO
// let ser = SD.SDG.SDGSERIES.Aadgr

// requirements section
// include scenarios, 
// refference stat.ee example from new document and try to replicate report produced by stat.ee office
// this is help for validation 

// Work with sdmx xmls
// What is the right way to look up information required 
// 1. (Dataflow Names), Agency Id 
// 2. Lookup Dimensions in CodeLists structures
// What is 


// SD.SDG.SDGFREQ // error

// let c = SD.GetDataContext()
// let dataflows = c.Dataflows

// dataflows.``World Development Indicators``

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
