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

    let private retryCount = 5
    let private parallelIndicatorPageDownloads = 8
    
    // todo make internal again
    type DimensionValueRecord =
        { Id : string
          Name: string}
      
    type internal DimensionRecord =
        { 
          DimensionName : string
          DataStructureId : string
          AgencyId : string
          Id : string
          EnumerationId: string
          Position: string
          DimensionValues : DimensionValueRecord seq}

    type internal DataflowRecord =
        { Id : string
          Name : string 
          AgencyID: string
          Version: string }

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
                        // printfn "Query New doc"
                        // let! doc = Http.AsyncRequestString(url)
                        printfn "url: %s" url
                        let response = Http.Request(url)// raises error on 404                                             
                        let bodyText = 
                            match response.StatusCode with
                            | 200 ->
                                match response.Body with
                                | Text text -> text
                                // if not (String.IsNullOrEmpty response.Body) then
                                //     restCache.Set(url, response.Body)
                                // printfn "Length: %i" (String.length response.Body)
                                // return response.StatusCode
                                | Binary bytes -> 
                                    string bytes.Length
                            | 307 -> 
                                printfn "Response 307"
                                "-"
                            | 404 -> 
                                printfn "Response 404"
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
            // printfn "getDimensions agencyId: %s - dataflowId: %s " agencyId dataflowId
            // helper functions
            let xn (s:string) = XName.Get(s)
            let xmes (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message")
            let xstr (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure")
            let xcom (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common")
        
            async { 
                // printfn "Download Datastructure: %s" (sdmxDatastructuresUrl agencyId dataflowId)
                // let xml = Http.RequestString("https://api.worldbank.org/v2/sdmx/rest/datastructure/WB/WDI/1.0/?references=children")
                // let xml = Http.RequestString(sdmxDatastructuresUrl agencyId dataflowId)
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
                    let dataStructuresElement = structuresElements.Element(xstr "DataStructures")
                    let dataStructureElement = dataStructuresElement.Element(xstr "DataStructure")
                    let datastructureId = dataStructureElement.Attribute(xn "id").Value
                    let dimensionListElement = dataStructureElement.Element(xstr "DataStructureComponents").Element(xstr "DimensionList")
                    let dimensionElements = dimensionListElement.Elements(xstr "Dimension")
                    // TODO use 
                    let timeDimension = dimensionListElement.Element(xstr "TimeDimension")

                    let dimensions = 
                        seq {
                            for dimensionElement in dimensionElements do
                                let dimensionId = dimensionElement.Attribute(xn "id")
                                let enumerationRefId = dimensionElement.Element(xstr "LocalRepresentation").Element(xstr "Enumeration").Element(xn "Ref").Attribute(xn "id")
                                let enumerationRefAgencyId = dimensionElement.Element(xstr "LocalRepresentation").Element(xstr "Enumeration").Element(xn "Ref").Attribute(xn "agencyID")
                                let positionAttribute = dimensionElement.Attribute(xn "position")            
                                yield dimensionId.Value, enumerationRefAgencyId.Value, enumerationRefId.Value, positionAttribute.Value
                        }
                    let d = [ 
                        for dimensionId, enumerationRefAgencyId, enumerationRefId, position in dimensions do
                            let dimensionOption = codelistElements |> Seq.tryFind (fun xe -> xe.Attribute(xn "id").Value = enumerationRefId)
                            match dimensionOption with
                            | Some dimensionValue -> 
                                let dimensionCodes = dimensionValue.Elements(xstr "Code")
                                let dimensionName = dimensionValue.Element(xcom "Name").Value
                                let dimensionRecords = seq {
                                    for dimensionCode in dimensionCodes do
                                        let dimensionValue = dimensionCode.Element(xcom "Name").Value
                                        let dimensionValueId = dimensionCode.Attribute(xn "id").Value
                                        yield {
                                            Id=dimensionValueId
                                            Name=dimensionValue
                                        }
                                }
                                yield {
                                    DimensionName=dimensionName
                                    DataStructureId=datastructureId 
                                    AgencyId=enumerationRefAgencyId
                                    Id=dimensionId
                                    EnumerationId=enumerationRefId
                                    Position=position
                                    DimensionValues=Seq.toList dimensionRecords
                                }
                    ]
                    d
            }
        
        let getDataflows(args) =
            // https://api.worldbank.org/v2/sdmx/rest/dataflow/all/all/latest/
            // helper functions
            printfn "Getting Dataflows"
            let xn (s:string) = XName.Get(s)
            let xmes (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message")
            let xstr (tag:string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/structure")
            let xcom (tag: string) = XName.Get(tag, "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/common")

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
                        yield {
                            Id = dataflowId
                            Name = dataflowDisplayName
                            AgencyID = dataflowAgencyId
                            Version = dataflowVersion
                        }
                    ]}                    

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
        member internal __.GetDimensionsAsync(agencyId, dataflowId) = 
            async { let! data = getDimensions agencyId dataflowId                        
                    return data }
        member internal x.GetDimensions(agencyId, dataflowId) =
             x.GetDimensionsAsync(agencyId, dataflowId) |> Async.RunSynchronously

[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
type DimensionValue internal (connection:ServiceConnection, dimensionId:string) =    
    member x.Id = dimensionId

/// [omit]
type IDimensionValue =
    abstract GetDimensionValue : indicatorCode:string -> DimensionValue
    abstract AsyncGetDimensionValue : indicatorCode:string -> Async<DimensionValue>
    

[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Dimension data
type Dimension internal (connection:ServiceConnection, dimensionId:string) =    
    let dimensionValues = seq { for dimensionValue in [] -> DimensionValue(connection, dimensionValue.Id) }
    member x.Id = dimensionId
    // member x.Name = connection.DimensionsIndexed.[dimensionId].Id
    member x.Description = "Test"
    
    interface IDimensionValue with
        member x.GetDimensionValue(dimensionId) = DimensionValue(connection, dimensionId)
        member x.AsyncGetDimensionValue(dimensionId) = async { return DimensionValue(connection, dimensionId) }
    interface seq<DimensionValue> with member x.GetEnumerator() = dimensionValues.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = dimensionValues.GetEnumerator() :> _

/// [omit]
type IDimensions =
    abstract GetDimension : dimensionId:string -> Dimension
    abstract AsyncGetDimension : dimensionId:string -> Async<Dimension>

/// [omit]
type Dimensions internal (connection:ServiceConnection, dataflowId) =
    // TODO filter by dataflowId
    let dimensions = seq { for dimension in connection.Dimensions -> Dimension(connection, dimension.Id) }
    interface IDimensions with
        member x.GetDimension(dimensionId) = Dimension(connection, dimensionId)
        member x.AsyncGetDimension(dimensionId) = async { return Dimension(connection, dimensionId) }
    interface seq<Dimension> with member x.GetEnumerator() = dimensions.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = dimensions.GetEnumerator() :> _

/// [omit]
type IDataflow =
    abstract GetDimensions : unit -> Dimensions


[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Metadata for a Dataflow
type Dataflow internal (connection:ServiceConnection, dataflowId:string) =
    let dimensions = new Dimensions(connection, dataflowId)
    /// Get the WorldBank code of the country
    member x.Id = dataflowId
    /// Get the name of the country
    // member x.Name = connection.DataflowsIndexed.[dataflowId].Name
    interface IDataflow with member x.GetDimensions() = dimensions

/// [omit]
type IDataflowCollection =
    abstract GetDataflow : dataflowId:string * dataflowName:string -> Dataflow


/// [omit]
type IDimensionCollection =
    abstract GetDimension : dimensionId:string -> Dimension

/// [omit]
type DataflowCollection<'T when 'T :> Dataflow> internal (serviceUrl: string) =
    let restCache = createInternetFileCache "SdmxRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl)
    let items =
        seq { let dataflows = connection.Dataflows
              for dataflow in dataflows do              
                  yield Dataflow(connection, dataflow.Id) :?> 'T }
    interface seq<'T> with member x.GetEnumerator() = items.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = (items :> IEnumerable).GetEnumerator()
    interface IDataflowCollection with member x.GetDataflow(dataflowId, (*this parameter is only here to help FunScript*)_dataflowName) = Dataflow(connection, dataflowId)

/// [omit]
type DimensionCollection<'T when 'T :> Dimension> internal (connection: ServiceConnection) =
    let items =
        seq { let dimensions = connection.Dimensions
              for dimension in dimensions do
                  yield Dimension(connection, dimension.Id) :?> 'T }
    interface seq<'T> with member x.GetEnumerator() = items.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = (items :> IEnumerable).GetEnumerator()
    interface IDimensionCollection with member x.GetDimension(dimensionId) = Dimension(connection, dimensionId)


/// [omit]
type ISdmxData =
    abstract GetDataflows<'T when 'T :> Dataflow> : unit -> seq<'T>
    abstract GetDimensions<'T when 'T :> Dimension> : unit -> seq<'T>

/// [omit]
type SdmxData(serviceUrl:string) =
    let restCache = createInternetFileCache "SdmxRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl)
    interface ISdmxData with
        member x.GetDataflows() = DataflowCollection("") :> seq<_>
        member x.GetDimensions() = DimensionCollection(connection) :> seq<_>


type DataObject1() =
    let data = Dictionary<string,obj>()
    let items = seq {for i in 1 .. 10 -> i * i}
    member x.RuntimeOperation() = data.Count
    member x.Values = items
    interface seq<int> with member x.GetEnumerator() = items.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = (items.GetEnumerator() :> _)


[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Dataflow data
type DataFlowObject(serviceUrl: string, dataflowId: string) =
    let restCache = createInternetFileCache "SdmxRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl)

    member x.DataflowId = dataflowId
    member x.Name = connection.DataflowsIndexed.[dataflowId].Name
    member x.AgencyId = connection.DataflowsIndexed.[dataflowId].AgencyID
    member x.Version = connection.DataflowsIndexed.[dataflowId].Version




[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Dataflow data
type DimensionObject(serviceUrl: string, agencyId:string, dataflowId: string, dimensionId: string) =
    let restCache = createInternetFileCache "SdmxRuntime" (TimeSpan.FromDays 30.0)
    let connection = new ServiceConnection(restCache, serviceUrl)

    let dimensions = connection.DimensionsIndexed agencyId dataflowId

    let items = seq { for i in dimensions.[dimensionId].DimensionValues do yield (i.Id, i.Name) }
    

    member x.DataflowId = dataflowId
    member x.Id = dimensions.[dimensionId].Id
    member x.Name = dimensions.[dimensionId].DimensionName
    member x.DataStructureId = dimensions.[dimensionId].DataStructureId
    member x.EnumerationId = dimensions.[dimensionId].EnumerationId
    member x.Position = dimensions.[dimensionId].Position

    interface seq<string * string> with member x.GetEnumerator() = items.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = (items.GetEnumerator() :> _)
