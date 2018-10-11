# SdmxProvider


```
#r @"../../../bin/lib/net45/FSharp.Data.dll"

open FSharp.Data
type SD = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">

let wb = SD.GetDataContext()

wb.Dataflows.SDG.Dimensions.FREQ

```    