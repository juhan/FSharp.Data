// --------------------------------------------------------------------------------------
// Sdmx type provider - runtime components
// --------------------------------------------------------------------------------------

namespace FSharp.Data.Runtime.Sdmx

open System
open System.Collections
open System.Diagnostics
open System.Globalization
open System.Xml.Linq
open System.Net
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.Caching

[<AutoOpen>]
/// [omit]
module Implementation =

    let xn (s:string) = XName.Get(s)
    let xmes (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message")
    let xstr (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure")
    let xgen (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic")
    let xcom (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common")
    
    type ContactRecord =
        {Name : string
         Email : string}

    type SenderRecord =
        {Name: string
         Contact: ContactRecord}

    type HeaderRecord =
        {ID : string
         Test : string
         Prepared : string
         Sender : SenderRecord}

    // todo make internal again
    type DimensionValueRecord =
        { Id : string
          Name: string}
      
    type internal DimensionRecord =
        { Name : string
          Description : string
          DataStructureId : string
          AgencyId : string
          Id : string
          EnumerationId: string
          Position: string
          Values : DimensionValueRecord seq
          Header : HeaderRecord}

    type internal DataflowRecord =
        { Id : string
          Name : string
          DataId : string
          AgencyID : string
          Version : string}

    type internal ServiceConnection(restCache:ICache<_,_>,serviceUrl:string) =               

        let sdmxUrl (functions: string list) (props: (string * string) list) =
            let url =
                serviceUrl::(List.map Uri.EscapeUriString functions)
                |> String.concat "/"
            let url = url + "/"
            let query = props            
            let requestUrl = Http.AppendQueryToUrl(url, query)            
            requestUrl

        let sdmxRequest resouces queryParams : Async<string> =
            async {
                let url = sdmxUrl resouces queryParams
                match restCache.TryRetrieve(url) with
                | Some res -> 
                    printfn "Return Cached value for url: %s" url
                    return res
                | None ->
                    try
                        // let! doc = Http.AsyncRequestString(url)
                        printfn "url: %s" url
                        let response = Http.Request(url)// raises error on 404                                             
                        let bodyText = 
                            match response.StatusCode with
                            | 200 ->
                                match response.Body with
                                | Text text -> 
                                    if not (String.IsNullOrEmpty text) then
                                        restCache.Set(url, text)
                                    text
                                | Binary bytes -> 
                                    printfn "Binary Response Length %s " (string bytes.Length)
                                    "-"
                            | status   -> 
                                printfn "Response %i" status
                                "-"
                        restCache.Set(url, bodyText)
                        return bodyText
                    with e ->  
                        printfn "Failed request %s" e.Message                       
                        return "-"
            }       

        let rec getSdmxDocuments resourceIdentificators queryParams: Async<string> =
            async {
                let! data = sdmxRequest resourceIdentificators queryParams
                return data
            }            
        
        let getDimensions agencyId dataflowId =
            async { 
                let! dimensionsXml = getSdmxDocuments ["datastructure"; agencyId; dataflowId; "1.0"] [ "references", "children"]                              
                return match dimensionsXml with
                        | "-" -> []
                        | _ ->
                            let xd = XDocument.Parse(dimensionsXml)
                            let rootElement = xd.Root
                            let headerElement = rootElement.Element(xmes "Header")
                            let structuresElements = rootElement.Element(xmes "Structures")
                            let codelistsElement = structuresElements.Element(xstr "Codelists")
                            let codelistElements = codelistsElement.Elements(xstr "Codelist")
                            let conceptsElement = structuresElements.Element(xstr "Concepts")
                            let conceptsSchemeElement = conceptsElement.Element(xstr "ConceptScheme")
                            let conceptsElements = conceptsSchemeElement.Elements(xstr "Concept")
                            let dataStructuresElement = structuresElements.Element(xstr "DataStructures")
                            let dataStructureElement = dataStructuresElement.Element(xstr "DataStructure")
                            let datastructureId = dataStructureElement.Attribute(xn "id").Value
                            let dimensionListElement = dataStructureElement.Element(xstr "DataStructureComponents").Element(xstr "DimensionList")
                            let dimensionElements = dimensionListElement.Elements(xstr "Dimension")

                            let header = {
                                ID="1"
                                Test=""
                                Prepared=""
                                Sender={Name=""; Contact={Name=""; Email=""}}
                            }

                            let dimensions = 
                                seq {
                                    for dimensionElement in dimensionElements do
                                        let dimensionId = dimensionElement.Attribute(xn "id")
                                        let enumerationRefId = dimensionElement.Element(xstr "LocalRepresentation").Element(xstr "Enumeration").Element(xn "Ref").Attribute(xn "id")
                                        let enumerationRefAgencyId = dimensionElement.Element(xstr "LocalRepresentation").Element(xstr "Enumeration").Element(xn "Ref").Attribute(xn "agencyID")
                                        let positionAttribute = dimensionElement.Attribute(xn "position")
                                        let conceptIdAttribute = dimensionElement.Element(xstr "ConceptIdentity").Element(xn "Ref").Attribute(xn "id")
                                        yield dimensionId.Value, enumerationRefAgencyId.Value, enumerationRefId.Value, positionAttribute.Value, conceptIdAttribute.Value
                                }
                            let d = [ 
                                for dimensionId, enumerationRefAgencyId, enumerationRefId, position, conceptId  in dimensions do
                                    let dimensionOption = codelistElements |> Seq.tryFind (fun xe -> xe.Attribute(xn "id").Value = enumerationRefId)
                                    let dimensionConceptOption = conceptsElements |> Seq.tryFind (fun xe -> xe.Attribute(xn "id").Value = conceptId)
                                   
                                    match dimensionOption,  dimensionConceptOption with
                                    | Some dimensionValue, Some conceptValue -> 
                                        let dimensionCodes = dimensionValue.Elements(xstr "Code")
                                        let dimensionName = conceptValue.Element(xcom "Name").Value
                                        let dimensionDescription =
                                            match conceptValue.Element(xcom "Description") with
                                            | x when not(x = null) -> x.Value
                                            | _ -> "No Description"
                                        let dimensionRecords = seq {
                                            for dimensionCode in dimensionCodes do
                                                let dimensionValue = dimensionCode.Element(xcom "Name").Value
                                                let dimensionValueId = dimensionCode.Attribute(xn "id").Value
                                                yield {
                                                    Id=dimensionValueId
                                                    Name=dimensionValue+"_"+dimensionValueId
                                                }
                                        }
                                        yield {
                                            Name=dimensionName
                                            Description=dimensionDescription
                                            DataStructureId=datastructureId 
                                            AgencyId=enumerationRefAgencyId
                                            Id=dimensionId
                                            EnumerationId=enumerationRefId
                                            Position=position
                                            Values=Seq.toList dimensionRecords
                                            Header=header
                                        }
                                    | _ -> failwith "Faild to match %s %s" dimensionOption.Value dimensionConceptOption.Value
                            ]
                            d
            }
        
        let getDataflows(args) =
            async {
                let! dataflowsXml = getSdmxDocuments ["dataflow"; "all"; "all"; "latest"] []
                // printfn "dataflowsXml Dataflows"
                let xd = XDocument.Parse(dataflowsXml)
                let rootElement = xd.Root
                let headerElement = rootElement.Element(xmes "Header")
                let structuresElements = rootElement.Element(xmes "Structures")
                let dataflowsEelements = structuresElements.Element(xstr "Dataflows").Elements(xstr "Dataflow")
                return 
                    [ for dataflowsEelement in dataflowsEelements do
                        let structureElement = dataflowsEelement.Element(xstr "Structure")
                        let refElement = structureElement.Element(xn "Ref")
                        let dataflowDisplayName:string = dataflowsEelement.Element(xcom "Name").Value.Trim()
                        let dataflowId:string = refElement.Attribute(xn "id").Value
                        let dataflowAgencyId:string = refElement.Attribute(xn "agencyID").Value
                        let dataflowVersion:string = refElement.Attribute(xn "version").Value

                        let dataId = dataflowsEelement.Attribute(xn "id").Value

                        yield {
                            Id = dataflowId
                            DataId = dataId
                            Name = dataflowDisplayName
                            AgencyID = dataflowAgencyId
                            Version = dataflowVersion
                        }
                    ]}
                    
        let getData flowRef key = 
            async { let! dataXml = getSdmxDocuments ["data"; flowRef; key; "all"] []
                    let xd = XDocument.Parse(dataXml)
                    let rootElement = xd.Root
                    let headerElement = rootElement.Element(xmes "Header")
                    let dataSetElement = rootElement.Element(xmes "DataSet")
                    let seriesElement = dataSetElement.Element(xgen "Series")
                    let obsElements = seriesElement.Elements(xgen "Obs")
                    return
                        [ for obsElement in obsElements do
                            let obsDimensionElement = obsElement.Element(xgen "ObsDimension")
                            let obsDimensionValue = obsDimensionElement.Attribute(xn "value")
                            let obsValueElement = obsElement.Element(xgen "ObsValue")
                            let obsValue = obsValueElement.Attribute(xn "value")
                            match obsDimensionValue, obsValue with
                            | null, null -> printfn "Error no A B"
                            | a,    null -> printfn "Error no B - %s" a.Value
                            | null, b    -> printfn "Error no A -  %s" b.Value
                            | a,    b    -> yield (a.Value, b.Value)]}

        /// At compile time, download the schema
        let dimensions = lazy (getDimensions "" "" |> Async.RunSynchronously |> List.toSeq |> Seq.distinctBy (fun i -> i.Id) |> Seq.toList)
        let dimensionsIndexed agencyId dataflowId = lazy (getDimensions agencyId dataflowId |> Async.RunSynchronously  |> Seq.map (fun i -> i.Id, i) |> dict)                
        let dataflows = lazy (getDataflows [] |> Async.RunSynchronously)
        let dataflowsIndexed = lazy (dataflows.Force() |> Seq.map (fun c -> c.Id, c) |> dict)

        member internal __.Dimensions = dimensions.Force()
        member internal __.DimensionsByDataflow = dimensions.Force()
        member internal __.DimensionsIndexed agencyId dataflowId = (dimensionsIndexed agencyId dataflowId).Force()
        member internal __.Dataflows = dataflows.Force()
        member internal __.DataflowsIndexed = dataflowsIndexed.Force()

        /// At runtime, download the data
        member internal __.GetDataAsync(flowRef, key) = 
            async {
                    let! data = getData flowRef key
                    return 
                      seq { for k, v in data do
                              if not (String.IsNullOrEmpty v) then 
                                 yield int k, float v } 
                      // It's a time series - sort it :-)  We should probably also interpolate (e.g. see R time series library)
                      |> Seq.sortBy fst } 

        member internal x.GetData(flowRef, dimensions) = 
             x.GetDataAsync(flowRef, dimensions) |> Async.RunSynchronously
        member internal __.GetDimensionsAsync(agencyId, dataflowId) = 
            async { let! data = getDimensions agencyId dataflowId                        
                    return data }
        member internal x.GetDimensions(agencyId, dataflowId) =
             x.GetDimensionsAsync(agencyId, dataflowId) |> Async.RunSynchronously

[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Dataflow data
type DimensionObject(serviceUrl: string, agencyId:string, dataflowId: string, dimensionId: string, dimensionValueId: string) =
    let restCache = createInternetFileCache "SdmxRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl)

    let dimensions = connection.DimensionsIndexed agencyId dataflowId

    let items = seq { for i in dimensions.[dimensionId].Values do yield (i.Id, i.Name) }
    

    member x.DataflowId = dataflowId
    member x.Id = dimensions.[dimensionId].Id
    member x.Name = dimensions.[dimensionId].Name
    member x.DataStructureId = dimensions.[dimensionId].DataStructureId
    member x.EnumerationId = dimensions.[dimensionId].EnumerationId
    member x.Position = dimensions.[dimensionId].Position

    member x.DimensionValueId = dimensionValueId
    member x.DimensionValueName = items |> Seq.tryFind (fun (a, b) -> a = dimensionValueId)

    interface seq<string * string> with member x.GetEnumerator() = items.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = (items.GetEnumerator() :> _)

[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Dataflow data
type DataFlowObject(serviceUrl: string, dataflowId: string, ?dimensions: list<DimensionObject>) =
    let restCache = createInternetFileCache "SdmxRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl)

    let data = match dimensions with
               | Some dim ->
                            let key = dim
                                      |> Seq.sortBy (fun arg -> int arg.Position)
                                      |> Seq.map (fun e -> e.DimensionValueId )
                                      |> Seq.toList |> String.concat "."
                            connection.GetData(dataflowId, key) |> Seq.cache
               | _ -> Seq.empty
    
    //let dataDict = lazy (dict data)

    member x.DataflowId = dataflowId
    member x.Name = connection.DataflowsIndexed.[dataflowId].Name
    member x.AgencyId = connection.DataflowsIndexed.[dataflowId].AgencyID
    member x.Version = connection.DataflowsIndexed.[dataflowId].Version
    member x.Data = data



