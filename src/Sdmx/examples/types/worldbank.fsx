#r @"../../../bin/lib/net45/FSharp.Data.dll"
// #r "System.Xml.Linq.dll"
open FSharp.Data

type WorldBank = WorldBankDataProvider<"World Development Indicators", Asynchronous=true>
let wb = WorldBank.GetDataContext()

wb.Countries.Togo.CapitalCity
// wb.Countries.``Upper middle income``.Indicators.``2005 PPP conversion factor, GDP (LCU per international $)``.

