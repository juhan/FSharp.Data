#r @"../../../bin/lib/net45/FSharp.Data.dll"
// #r "System.Xml.Linq.dll"
open FSharp.Data

type WorldBank = WorldBankDataProvider<"World Development Indicators", Asynchronous=true>
let wb = WorldBank.GetDataContext()
wb.Topics
let countries = wb.Countries.Togo.Indicators
wb.Countries.Togo.Code
wb.Countries.Togo.Name
wb.Countries.Togo.Region
wb.Countries.Togo.Indicators.``Account (% age 15+)``

wb.Topics.Education.Indicators.``Account (% age 15+)``.IndicatorCode


for country in countries do
    printfn "Country %s " country.Name

// wb.Countries.``Upper middle income``.Indicators.``2005 PPP conversion factor, GDP (LCU per international $)``.

