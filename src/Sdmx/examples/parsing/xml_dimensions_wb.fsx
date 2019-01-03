open System
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

let xml2 = """
<Structure
	xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message">
	<Header>
		<ID>IDREF3</ID>
		<Test>false</Test>
		<Prepared>2018-08-30T18:10:58.1327544-04:00</Prepared>
		<Sender id="Unknown" />
		<Receiver id="Unknown" />
	</Header>
	<Structures>
		<Codelists
			xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure">
			<Codelist id="CL_FREQ_WDI" urn="urn:sdmx:org.sdmx.infomodel.codelist.Codelist=WB:CL_FREQ_WDI(1.0)" agencyID="WB" version="1.0" isFinal="true">
				<Name xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">
					Frequency code list
				</Name>
				<Code id="A" urn="urn:sdmx:org.sdmx.infomodel.codelist.Code=WB:CL_FREQ_WDI(1.0).A">
					<Name xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">
						Annual
					</Name>
				</Code>
				<Code id="2A" urn="urn:sdmx:org.sdmx.infomodel.codelist.Code=WB:CL_FREQ_WDI(1.0).2A">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Two-year average
					</Name>
				</Code>
				<Code id="3A" urn="urn:sdmx:org.sdmx.infomodel.codelist.Code=WB:CL_FREQ_WDI(1.0).3A">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Three-year average
					</Name>
				</Code>							
			</Codelist>
		</Codelists>

		<Concepts xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure">
			<ConceptScheme id="WDI_CONCEPT" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.ConceptScheme=WB:WDI_CONCEPT(1.0)" agencyID="WB" version="1.0" isFinal="true">
				<Name xml:lang="en"
					xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Default Scheme
				</Name>
				<Concept id="FREQ" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=WB:WDI_CONCEPT(1.0).FREQ">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Frequency
					</Name>
					<Description xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Indicates rate of recurrence at which observations occur (e.g. monthly, yearly, biannually, etc.).
					</Description>
				</Concept>
				<Concept id="REF_AREA" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=WB:WDI_CONCEPT(1.0).REF_AREA">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Reference Area
					</Name>
					<Description xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Reference Area: Specific areas (e.g. Country, Regional Grouping, etc) the observed values refer to. Reference areas can be determined according to different criteria (e.g.: geographical, economic, etc.).</Description>
				</Concept>
				<Concept id="SERIES" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=WB:WDI_CONCEPT(1.0).SERIES">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Series
					</Name>
					<Description xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">The phenomenon or phenomena to be measured in the data set. The word SERIES is used for consistency with the term used in the WDI Database. SERIES are 54 key indicators from WDI </Description>
				</Concept>
				<Concept id="TIME_PERIOD" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=WB:WDI_CONCEPT(1.0).TIME_PERIOD">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Time period
					</Name>
					<Description xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Reference date - or date range - the observed value refers to (usually different from the dates of data production or dissemination). </Description>
				</Concept>
				<Concept id="UNIT_MULT" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=WB:WDI_CONCEPT(1.0).UNIT_MULT">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Unit multiplier
					</Name>
					<Description xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Exponent in base 10 that multiplied by the observation numeric value gives the result expressed in the unit of measure. </Description>
				</Concept>
				<Concept id="OBS_VALUE" urn="urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=WB:WDI_CONCEPT(1.0).OBS_VALUE">
					<Name xml:lang="en"
						xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Observation Value
					</Name>
					<Description xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">Observation value </Description>
				</Concept>
			</ConceptScheme>
		</Concepts>

		<DataStructures
			xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure">
			<DataStructure id="WDI" urn="urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure=WB:WDI(1.0)" agencyID="WB" version="1.0" isFinal="true">
				<Name xml:lang="en" xmlns="http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common">
					World Development Indicators
				</Name>
				<DataStructureComponents>
					<DimensionList id="DimensionDescriptor" urn="urn:sdmx:org.sdmx.infomodel.datastructure.DimensionDescriptor=WB:WDI(1.0).DimensionDescriptor">
						<Dimension id="FREQ" urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dimension=WB:WDI(1.0).FREQ" position="1">
							<ConceptIdentity>
								<Ref id="FREQ" maintainableParentID="WDI_CONCEPT" maintainableParentVersion="1.0" agencyID="WB" package="conceptscheme" class="Concept" xmlns="" />
							</ConceptIdentity>
								<LocalRepresentation>
									<Enumeration>
										<Ref id="CL_FREQ_WDI" version="1.0" agencyID="WB" package="codelist" class="Codelist" xmlns="" />
									</Enumeration>
								</LocalRepresentation>
						</Dimension>
						
						<Dimension id="SERIES" urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dimension=WB:WDI(1.0).SERIES" position="2">
							<ConceptIdentity>
								<Ref id="SERIES" maintainableParentID="WDI_CONCEPT" maintainableParentVersion="1.0" agencyID="WB" package="conceptscheme" class="Concept" xmlns="" />
							</ConceptIdentity>
							<LocalRepresentation>
								<Enumeration>
									<Ref id="CL_SERIES_WDI" version="1.0" agencyID="WB" package="codelist" class="Codelist" xmlns="" />
								</Enumeration>
							</LocalRepresentation>
						</Dimension>
						
						<Dimension id="REF_AREA" urn="urn:sdmx:org.sdmx.infomodel.datastructure.Dimension=WB:WDI(1.0).REF_AREA" position="3">
							<ConceptIdentity>
								<Ref id="REF_AREA" maintainableParentID="WDI_CONCEPT" maintainableParentVersion="1.0" agencyID="WB" package="conceptscheme" class="Concept" xmlns="" />
							</ConceptIdentity>
							<LocalRepresentation>
								<Enumeration> <Ref id="CL_REF_AREA_WDI" version="1.0" agencyID="WB" package="codelist" class="Codelist" xmlns="" /> </Enumeration>
							</LocalRepresentation>
						</Dimension>
						
						<TimeDimension id="TIME_PERIOD" urn="urn:sdmx:org.sdmx.infomodel.datastructure.TimeDimension=WB:WDI(1.0).TIME_PERIOD" position="4">
							<ConceptIdentity>
								<Ref id="TIME_PERIOD" maintainableParentID="WDI_CONCEPT" maintainableParentVersion="1.0" agencyID="WB" package="conceptscheme" class="Concept"
									xmlns="" />
							</ConceptIdentity>
							<LocalRepresentation>
								<TextFormat textType="ObservationalTimePeriod" />
							</LocalRepresentation>
						</TimeDimension>

					</DimensionList>
					
					<AttributeList id="AttributeDescriptor" urn="urn:sdmx:org.sdmx.infomodel.datastructure.AttributeDescriptor=WB:WDI(1.0).AttributeDescriptor">
						<Attribute id="UNIT_MULT" urn="urn:sdmx:org.sdmx.infomodel.datastructure.DataAttribute=WB:WDI(1.0).UNIT_MULT" assignmentStatus="Mandatory">
							<ConceptIdentity>
								<Ref id="UNIT_MULT" maintainableParentID="WDI_CONCEPT" maintainableParentVersion="1.0" agencyID="WB" package="conceptscheme" class="Concept" xmlns="" />
							</ConceptIdentity>
							<LocalRepresentation>
								<Enumeration>
									<Ref id="CL_UNIT_MULT_WDI" version="1.0" agencyID="WB" package="codelist" class="Codelist" xmlns="" />
								</Enumeration>
							</LocalRepresentation>
							<AttributeRelationship>
								<PrimaryMeasure>
									<Ref id="OBS_VALUE" xmlns="" />
								</PrimaryMeasure>
							</AttributeRelationship>
						</Attribute>
					</AttributeList>

					<MeasureList id="MeasureDescriptor" urn="urn:sdmx:org.sdmx.infomodel.datastructure.MeasureDescriptor=WB:WDI(1.0).MeasureDescriptor">
						<PrimaryMeasure id="OBS_VALUE" urn="urn:sdmx:org.sdmx.infomodel.datastructure.PrimaryMeasure=WB:WDI(1.0).OBS_VALUE">
							<ConceptIdentity>
								<Ref id="OBS_VALUE" maintainableParentID="WDI_CONCEPT" maintainableParentVersion="1.0" agencyID="WB" package="conceptscheme" class="Concept" xmlns="" />
							</ConceptIdentity>
						</PrimaryMeasure>
					</MeasureList>

				</DataStructureComponents>
			</DataStructure>
		</DataStructures>
	</Structures>
</Structure>
"""

let xml = Http.RequestString("https://api.worldbank.org/v2/sdmx/rest/datastructure/WB/WDI/1.0/?references=children")
let xd = XDocument.Parse(xml)
let rootElement = xd.Root
let headerElement = rootElement.Element(xmes "Header")

let resX = rootElement.Element(xmes "Header1")
let res = not (resX = null)
res

let structuresElements = rootElement.Element(xmes "Structures")
let codelistsElement = structuresElements.Element(xstr "Codelists")
let codelistElements = codelistsElement.Elements(xstr "Codelist")
let conceptsElement = structuresElements.Element(xstr "Concepts")
let conceptsSchemeElement = conceptsElement.Element(xstr "ConceptScheme")
let conceptsElements = conceptsSchemeElement.Elements(xstr "Concept")
let dataStructuresElement = structuresElements.Element(xstr "DataStructures")
let dimensionListElement = dataStructuresElement.Element(xstr "DataStructure").Element(xstr "DataStructureComponents").Element(xstr "DimensionList")
let dimensionElements = dimensionListElement.Elements(xstr "Dimension")
// TODO use 
let timeDimension = dimensionListElement.Element(xstr "TimeDimension")

timeDimension

let dimensionRefs = 
    seq {
        for dimensionElement in dimensionElements do
            let enumerationId = dimensionElement.Element(xstr "LocalRepresentation").Element(xstr "Enumeration").Element(xn "Ref").Attribute(xn "id")
            let positionAttribute = dimensionElement.Attribute(xn "position")
            let conceptIdAttribute = dimensionElement.Element(xstr "ConceptIdentity").Element(xn "Ref").Attribute(xn "id")
            yield enumerationId.Value, positionAttribute.Value, conceptIdAttribute.Value
    }

let h = Seq.head dimensionRefs

let dimensionConceptOption = conceptsElements |> Seq.tryFind (fun xe -> xe.Attribute(xn "id").Value = "FREQ")
dimensionConceptOption


let dimensionRecords = seq { 
    for enumerationId, position, conceptId in dimensionRefs do
        let dimensionEnumerationOption = codelistElements |> Seq.tryFind (fun xe -> xe.Attribute(xn "id").Value = enumerationId)
        let dimensionConceptOption = conceptsElements |> Seq.tryFind (fun xe -> xe.Attribute(xn "id").Value = conceptId)
        match dimensionEnumerationOption, dimensionConceptOption with
        | Some enumerationValue, Some conceptValue -> 
            let dimensionCodes = enumerationValue.Elements(xstr "Code")
            for dimensionCode in dimensionCodes do
                let dimensionName = dimensionCode.Element(xcom "Name").Value
                yield (position, enumerationId, dimensionName)
        | None, None -> printfn ""
        | _ -> ""
}


for d in dimensionRecords do
    printfn "%A" d
