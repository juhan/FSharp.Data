#r @"../../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Xml.Linq.dll"
#load @"../../../../packages/test/FSharp.Charting/FSharp.Charting.fsx"
open FSharp.Data
open FSharp.Charting


// WorldBank
type WB = SdmxDataProvider<"https://api.worldbank.org/v2/sdmx/rest">
type WDI = WB.``World Development Indicators``

let data = WDI(WDI.Frequency.Annual_A,
               WDI.``Reference Area``.``United Kingdom_GBR``,
               WDI.Series.``Gross capital formation (% of GDP)_NE_GDI_TOTL_ZS``).Data


data |> Chart.Line
// ECB

type ECB = SdmxDataProvider<"http://a-sdw-wsrest.ecb.int/service">

type EXR = ECB.``Exchange Rates``

let ecbData = EXR(
        EXR.Frequency.Annual_A,
        EXR.Currency.``US dollar_USD``,
        EXR.``Currency denominator``.Euro_EUR,
        EXR.``Exchange rate type``.Spot_SP00,
        EXR.``Series variation - EXR context``.Average_A
        )
ecbData.Data |> Chart.Line
