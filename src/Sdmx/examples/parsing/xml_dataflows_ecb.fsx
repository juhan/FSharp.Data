#r @"../../../../bin/lib/net45/FSharp.Data.dll"
#r "System.Core.dll"
#r "System.Xml.Linq.dll"
open System.Xml.Linq
open FSharp.Data

// helper functions
let xn (s:string) = XName.Get(s)
let xmes (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message")
let xstr (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure")
let xcom (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common")

let xmls = """
<mes:Structure
	xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
	xmlns:xml="http://www.w3.org/XML/1998/namespace"
	xmlns:mes="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message"
	xmlns:str="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure"
	xmlns:com="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common" 
	xsi:schemaLocation="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message https://registry.sdmx.org/schemas/v2_1/SDMXMessage.xsd">
	<mes:Header>
		<mes:ID>IDREF173351</mes:ID>
		<mes:Test>false</mes:Test>
		<mes:Prepared>2018-12-09T09:38:09</mes:Prepared>
		<mes:Sender id="ECB" />
		<mes:Receiver id="not_supplied" />
	</mes:Header>
	<mes:Structures>
		<str:Dataflows>
			<str:Dataflow urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=ECB:AME(1.0)" isExternalReference="false" agencyID="ECB" id="AME" isFinal="false" version="1.0">
				<com:Name xml:lang="en">AMECO</com:Name>
				<str:Structure>
					<Ref package="datastructure" agencyID="ECB" id="ECB_AME1" version="1.0" class="DataStructure" />
				</str:Structure>
			</str:Dataflow>
			<str:Dataflow urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=ECB:BKN(1.0)" isExternalReference="false" agencyID="ECB" id="BKN" isFinal="false" version="1.0">
				<com:Name xml:lang="en">Banknotes statistics</com:Name>
				<str:Structure>
					<Ref package="datastructure" agencyID="ECB" id="ECB_BKN1" version="1.0" class="DataStructure" />
				</str:Structure>
			</str:Dataflow>
			<str:Dataflow urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=ECB:BLS(1.0)" isExternalReference="false" agencyID="ECB" id="BLS" isFinal="false" version="1.0">
				<com:Name xml:lang="en">Bank Lending Survey Statistics</com:Name>
				<str:Structure>
					<Ref package="datastructure" agencyID="ECB" id="ECB_BLS1" version="1.0" class="DataStructure" />
				</str:Structure>
			</str:Dataflow>
			<str:Dataflow urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=ECB:BOP(1.0)" isExternalReference="false" agencyID="ECB" id="BOP" isFinal="false" version="1.0">
				<com:Name xml:lang="en">Euro Area Balance of Payments and International Investment Position Statistics</com:Name>
				<str:Structure>
					<Ref package="datastructure" agencyID="ECB" id="ECB_BOP1" version="1.0" class="DataStructure" />
				</str:Structure>
			</str:Dataflow>								
		</str:Dataflows>
	</mes:Structures>
</mes:Structure>
"""
let xml = Http.RequestString("https://sdw-wsrest.ecb.europa.eu/service/dataflow/all/all/latest/")
let xd = XDocument.Parse(xml)

let rootElement = xd.Root
let headerElement = rootElement.Element(xmes "Header")
let structuresElements = rootElement.Element(xmes "Structures")

let dataflowsEelements = structuresElements.Element(xstr "Dataflows").Elements(xstr "Dataflow")

let dataflows = 
    seq {
        for element in dataflowsEelements do
            let dfId = element.Attribute(xn "id")
            let dfAgencyId = element.Attribute(xn "agencyID")
            let dfDisplayName = element.Element(xcom "Name").Value.Trim()
            yield dfId.Value, dfAgencyId.Value, dfDisplayName
    }

let sdg = Seq.head dataflows
let id, agencyId, displayName = sdg