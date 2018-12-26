open System
#r @"C:/Users/Demur/Documents/FSharp.Data/bin/lib/net45/FSharp.Data.dll"
#r "System.Core.dll"
#r "System.Xml.Linq.dll"
open System.Xml.Linq
open FSharp.Data

// helper functions
let xn (s:string) = XName.Get(s)
let xmes (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message")
let xstr (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure")
let xgen (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic")
let xcom (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common")


let xml = Http.RequestString("https://api.worldbank.org/v2/sdmx/rest/data/WDI/A.AG_LND_AGRI_K2.DEU/")

let xml2 = """
<message:GenericData
  xmlns:footer="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message/footer"
  xmlns:generic="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic"
  xmlns:message="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message"
  xmlns:common="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xmlns:xml="http://www.w3.org/XML/1998/namespace">
  <message:Header>
    <message:ID>WDI</message:ID>
    <message:Test>false</message:Test>
    <message:Prepared>2018-12-26T07:03:14</message:Prepared>
    <message:Sender id="WB">
      <common:Name xml:lang="en">World Bank Group</common:Name>
      <message:Contact>
        <common:Name xml:lang="en">SDMX API Admin</common:Name>
        <message:Email>data@worldbank.org</message:Email>
      </message:Contact>
    </message:Sender>
    <message:Structure structureID="WB_WDI_1_0" dimensionAtObservation="TIME_PERIOD">
      <common:Structure>
        <Ref agencyID="WB" id="WDI" version="1.0" />
      </common:Structure>
    </message:Structure>
    <message:DataSetAction>Information</message:DataSetAction>
    <message:DataSetID>WDI_WB_2018-12-26T070314</message:DataSetID>
    <message:Extracted>2018-12-26T07:03:14</message:Extracted>
  </message:Header>
  <message:DataSet action="Append" structureRef="WB_WDI_1_0">
    <generic:Series>
      <generic:SeriesKey>
        <generic:Value id="REF_AREA" value="DEU" />
        <generic:Value id="SERIES" value="AG_LND_AGRI_K2" />
        <generic:Value id="FREQ" value="A" />
      </generic:SeriesKey>
      <generic:Obs>
        <generic:ObsDimension id="TIME_PERIOD" value="1960" />
        <generic:ObsValue value="NaN" />
        <generic:Attributes>
          <generic:Value id="UNIT_MULT" value="0" />
        </generic:Attributes>
      </generic:Obs>
      <generic:Obs>
        <generic:ObsDimension id="TIME_PERIOD" value="1961" />
        <generic:ObsValue value="193750" />
        <generic:Attributes>
          <generic:Value id="UNIT_MULT" value="0" />
        </generic:Attributes>
      </generic:Obs>
      <generic:Obs>
        <generic:ObsDimension id="TIME_PERIOD" value="1962" />
        <generic:ObsValue value="193930" />
        <generic:Attributes>
          <generic:Value id="UNIT_MULT" value="0" />
        </generic:Attributes>
      </generic:Obs>
    </generic:Series>
  </message:DataSet>
</message:GenericData>
"""

let xd = XDocument.Parse(xml)
let rootElement = xd.Root
let headerElement = rootElement.Element(xmes "Header")
let dataSetElement = rootElement.Element(xmes "DataSet")
let seriesElement = dataSetElement.Element(xgen "Series")
let obsElements = seriesElement.Elements(xgen "Obs")

let obsElement = Seq.head obsElements

let data = seq {
    for obsElement in obsElements do
        let obsDimensionElement = obsElement.Element(xgen "ObsDimension")
        let obsDimensionValue = obsDimensionElement.Attribute(xn "value")
        let obsValueElement = obsElement.Element(xgen "ObsValue")
        let obsValue = obsValueElement.Attribute(xn "value")
        match obsDimensionValue, obsValue with
        | null, null -> printfn "Error no A B"
        | a,    null -> printfn "Error no B - %s" a.Value
        | null, b    -> printfn "Error no A -  %s" b.Value
        | a,    b    -> yield (a.Value, b.Value)
}
for a, b in data do
    printfn "%s, %s" a b

data |> Chart.Line

let obsDimensionElement = obsElement.Element(xgen "ObsDimension")
let obsDimensionValue = obsDimensionElement.Attribute(xn "value")

let obsValueElement = obsElement.Element(xgen "ObsValue")
let obsValue = obsValueElement.Attribute(xgen "value")
