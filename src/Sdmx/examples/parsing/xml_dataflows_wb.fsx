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


// From original xml removed 
// First line: <?xml version="1.0" encoding="utf-8"?>
// All xmlns="" attribues 
// Caused issuew while parsing 
let xml2 = """
<!--DDP Web Service v5.2.3-->
<Structure>
	<Header>
		<ID>IDREF6</ID>
		<Test>false</Test>
		<Prepared>2018-11-21T12:06:18.3786297-05:00</Prepared>
		<Sender id="Unknown" />
		<Receiver id="Unknown" />
	</Header>
	<Structures>
		<Dataflows>
		    <Dataflow id="SDG" urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=UNSD:SDG(1.0)" agencyID="UNSD" version="1.0" isFinal="false">
				<Name xml:lang="en"> SDG </Name>
				<Structure> <Ref id="SDG" version="0.4" agencyID="UNSD" package="datastructure" class="DataStructure" /> </Structure>
			</Dataflow>
			<Dataflow id="WDI" urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=WB:WDI(1.0)" agencyID="WB" version="1.0" isFinal="true">
				<Name xml:lang="en"> World Development Indicators </Name>
				<Structure> <Ref id="WDI" version="1.0" agencyID="WB" package="datastructure" class="DataStructure" /> </Structure>
			</Dataflow>
		</Dataflows>
	</Structures>
</Structure>
"""

let xml = Http.RequestString("https://api.worldbank.org/v2/sdmx/rest/dataflow/all/all/latest/")

let xd = XDocument.Parse(xml)

let rootElement = xd.Root
let headerElement = rootElement.Element(xmes "Header")
let structuresElements = rootElement.Element(xmes "Structures")

let dataflowsEelements = structuresElements.Element(xstr "Dataflows").Elements(xstr "Dataflow")

let dfEl = Seq.head dataflowsEelements

let structure = dfEl.Element(xstr "Structure")

let ref = structure.Element(xn "Ref")

let dataflows = 
    seq {
        for dataflowsEelement in dataflowsEelements do
            let structureElement = dataflowsEelement.Element(xstr "Structure")
            let refElement = structureElement.Element(xn "Ref")
            let dfId = refElement.Attribute(xn "id")
            let dfAgencyId = refElement.Attribute(xn "agencyID")
            let dfDisplayName = dataflowsEelement.Element(xcom "Name").Value.Trim()
            yield dfId.Value, dfAgencyId.Value, dfDisplayName
    }

let sdg = Seq.head dataflows
let id, agencyId, displayName = sdg
