#r @"../../../bin/lib/net45/FSharp.Data.dll"
// #r "System.Xml.Linq.dll"

open FSharp.Data
type SD = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">
let wb = SD.GetDataContext()
let dataflows = wb.Dataflows.SDG.
dataflows.

// SdmxDataProvider<"World Development Indicators", Asynchronous=true>
// let wb = SdmxData.GetDataContext()

// wb.WDI.
// wb.[Dataflow].[Dimention1].[Dimention2]


// wb.Countries.Togo.CapitalCity



let dimensionsType = 
    let someVal = "111"
    let fsn = fun aid () -> 
        printfn "From fsn [%s]" someVal
        []
    fsn


let dd = dimensionsType


<@
    let f x = x + 10
    f 20
@>