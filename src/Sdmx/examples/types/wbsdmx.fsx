#r @"../../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
open FSharp.Data
open System.IO
open System.Xml
open System.Xml.Linq
open System

// type ECB = SdmxDataProvider<"https://sdw-wsrest.ecb.europa.eu/service"> //"/dataflow/all/all/latest/"
// ECB.``Banknotes statistics``.ECB_BKN1BKN_DENOM.``20 cents``

type WB = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">

// WB.``World Development Indicators``.``Reference area code list``.Albania

// requirements section
// include scenarios, 
// refference stat.ee example from new document and try to replicate report produced by stat.ee office
// this is help for validation 

// Work with sdmx xmls
// What is the right way to look up information required 
// 1. (Dataflow Names), Agency Id 
// 2. Lookup Dimensions in CodeLists structures
// What is 

// SdmxDataProvider<"World Development Indicators", Asynchronous=true>
// let wb = SdmxData.GetDataContext()

// wb.WDI.
// wb.[Dataflow].[Dimention1].[Dimention2]


// wb.Countries.Togo.CapitalCity
// Tproblem statement > introduction
// mention javascript
//bibtext uppercase put in {}
// 3. Design of the sdmx
//  Datasource profile, to the provider for stat.ee
// went for xml because not all the providers support json and xml would give more coberage 
// 4. Evaluation, case study stat.ee 
// Debugging TypeProvider