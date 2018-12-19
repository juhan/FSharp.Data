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
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.Caching

[<AutoOpen>]
/// [omit]
module Implementation =


    let private retryCount = 5
    let private parallelIndicatorPageDownloads = 8

    type internal IndicatorRecord =
        { Id : string
          Name: string
          TopicIds : string list
          Source : string
          Description : string }

    type internal DimensionValueRecord =
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
          DimensionValues : DimensionValueRecord list}

    type internal CountryRecord =
        { Id : string
          Name : string
          CapitalCity : string
          Region : string }
        member x.IsRegion = x.Region = "Aggregates"

    type internal DataflowRecord =
        { Id : string
          Name : string 
          AgencyID: string
          Version: string }

    type internal TopicRecord =
        { Id : string
          Name : string
          Description : string }

    type internal ServiceConnection(restCache:ICache<_,_>,serviceUrl:string) =               
        let sources = List.empty        
        let sdmxUrl (functions: string list) (props: (string * string) list) =
            let url =
                serviceUrl::(List.map Uri.EscapeUriString functions)
                |> String.concat "/"
            let url = url + "/"
            let query = props            
            let requestUrl = Http.AppendQueryToUrl(url, query)            
            requestUrl

        // let sdmxDataflowsUrl = 
        //     let agencyId = "all"
        //     let resourceId = "all"
        //     let version = "latest"
        //     let allDataflows = serviceUrl + "/dataflow/all/all/latest/"
        //     let url = sprintf "%s/dataflow/%s/%s/%s/" serviceUrl agencyId resourceId version
        //     printfn "Dataflows url: %s" url
        //     url
        let sdmxDatastructuresUrl agencyId dataflowId = 
            // let agencyId = "WB"
            let resourceId = dataflowId
            let version = "1.0"
            let query = [ "references", "children"]
            // Structural metadata queries: 
            // https://ws-entry-point/resource/agencyID/resourceID/version/itemID?queryStringParameters
            let url = sprintf "%s/datastructure/%s/%s/%s/" serviceUrl agencyId resourceId version
            let q = Http.AppendQueryToUrl(url, query)
            printfn "Datastructure url: %s" q
            q

        // let sdmxRequest resourceName args= 
        //     // match resourceName with
        //     // | "dataflows" -> "Load all dataflows"
        //     // | "datastructures" -> "Load all datastructures"
        //     // | _ -> "fail"

        //     let doc = Http.AsyncRequestString(sdmxDataflowsUrl, headers = [ HttpRequestHeaders.UserAgent "F# Data WorldBank Type Provider"
        //                                                                     HttpRequestHeaders.Accept HttpContentTypes.Json ])
        //     doc
        
        // The WorldBank data changes very slowly indeed (monthly updates to values, rare updates to schema), hence caching it is ok.

        let sdmxRequest resouces queryParams : Async<string> =
            async {
                let url = sdmxUrl resouces queryParams
                // match restCache.TryRetrieve(url) with
                match None with
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
                        return bodyText
                    with e ->  
                        printfn "Failed request %s" e.Message                       
                        return "-"
            }
             
        let rec worldBankRequest attempt funcs args : Async<string> =
            async {
                let url = sdmxUrl funcs args
                match restCache.TryRetrieve(url) with
                | Some res -> return res
                | None ->
                    Debug.WriteLine (sprintf "[WorldBank] downloading (%d): %s" attempt url)
                    try
                        let! doc = Http.AsyncRequestString(url, headers = [ HttpRequestHeaders.UserAgent "F# Data WorldBank Type Provider"
                                                                            HttpRequestHeaders.Accept HttpContentTypes.Json ])
                        Debug.WriteLine (sprintf "[WorldBank] got text: %s" (if doc = null then "null" elif doc.Length > 50 then doc.[0..49] + "..." else doc))
                        if not (String.IsNullOrEmpty doc) then
                            restCache.Set(url, doc)
                        return doc
                    with e ->
                        Debug.WriteLine (sprintf "[WorldBank] error: %s" (e.ToString()))
                        if attempt > 0 then
                            return! worldBankRequest (attempt - 1) funcs args
                        else return! failwithf "Failed to request '%s'. Error: %O" url e }

        let rec getSdmxDocuments resourceIdentificators queryParams: Async<string> =
            async {
                let! data = sdmxRequest resourceIdentificators queryParams
                return data
            }
            

        let rec getDocuments funcs args page parallelPages =
            async { let! docs =
                        Async.Parallel
                            [ for i in 0 .. parallelPages - 1 ->
                                  worldBankRequest retryCount funcs (args @ ["page", string (page+i)]) ]
                    let docs = docs |> Array.map JsonValue.Parse
                    Debug.WriteLine (sprintf "[WorldBank] geting page count")
                    let pages = docs.[0].[0]?pages.AsInteger()
                    Debug.WriteLine (sprintf "[WorldBank] got page count = %d" pages)
                    if (pages < page + parallelPages) then
                        return Array.toList docs
                    else
                        let! rest = getDocuments funcs args (page + parallelPages) (pages - parallelPages)
                        return Array.toList docs @ rest }

        let getIndicators() =
            // Get the indicators in parallel, initially using 'parallelIndicatorPageDownloads' pages
            async { let! docs = getDocuments ["indicator"] [] 1 parallelIndicatorPageDownloads
                    return
                        [ for doc in docs do
                            for ind in doc.[1] do
                                let id = ind?id.AsString()
                                let name = ind?name.AsString().Trim([|'"'|]).Trim()
                                let sourceName = ind?source?value.AsString()
                                if sources = [] || sources |> List.exists (fun source -> String.Compare(source, sourceName, StringComparison.OrdinalIgnoreCase) = 0) then
                                    let topicIds = Seq.toList <| seq {
                                        for item in ind?topics do
                                            match item.TryGetProperty("id") with
                                            | Some id -> yield id.AsString()
                                            | None -> ()
                                    }
                                    let sourceNote = ind?sourceNote.AsString()
                                    yield { Id = id
                                            Name = name
                                            TopicIds = topicIds
                                            Source = sourceName
                                            Description = sourceNote} ] }

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

        let getDimensionValues() =
            async { return
                        [ for dimensionValue in [("A", "Annual"); 
                                                ("2A", "Two-year average");
                                                ("3A", "Three-year average");
                                                ("S", "Half-yearly, semester");
                                                ("Q", "Quarterly");
                                                ("M", "Monthly");
                                                ] do
                            let (id, name) = dimensionValue
                            yield { Id = id
                                    Name = name} ] }

        let getTopics() =
            async { let! docs = getDocuments ["topic"] [] 1 1
                    return
                        [ for doc in docs do
                            for topic in doc.[1] do
                                let id = topic?id.AsString()
                                let name = topic?value.AsString()
                                let sourceNote = topic?sourceNote.AsString()
                                yield { Id = id
                                        Name = name
                                        Description = sourceNote } ] }
                                        
        let getCountries(args) =
            async { let! docs = getDocuments ["country"] args 1 1
                    return
                        [ for doc in docs do
                            for country in doc.[1] do
                                let region = country?region?value.AsString()
                                let id = country?id.AsString()
                                let name = country?name.AsString()
                                let capitalCity = country?capitalCity.AsString()
                                yield { Id = id
                                        Name = name
                                        CapitalCity = capitalCity
                                        Region = region } ] }
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
        let getRegions() =
            async { let! docs = getDocuments ["region"] [] 1 1
                    return
                        [ for doc in docs do
                            for ind in doc.[1] do
                                yield ind?code.AsString(),
                                      ind?name.AsString() ] }

        let getData funcs args (key:string) =
            async { let! docs = getDocuments funcs args 1 1
                    return
                        [ for doc in docs do
                            for ind in doc.[1] do
                                yield ind.[key].AsString(),
                                      ind?value.AsString() ] }

        /// At compile time, download the schema
        let topics = lazy (getTopics() |> Async.RunSynchronously)
        let topicsIndexed = lazy (topics.Force() |> Seq.map (fun t -> t.Id, t) |> dict)
        let indicators = lazy (getIndicators() |> Async.RunSynchronously |> List.toSeq |> Seq.distinctBy (fun i -> i.Name) |> Seq.toList)
        let dimensions = lazy (getDimensions "" "" |> Async.RunSynchronously |> List.toSeq |> Seq.distinctBy (fun i -> i.Id) |> Seq.toList)
        let dimensionValues = lazy (getDimensionValues() |> Async.RunSynchronously |> List.toSeq |> Seq.distinctBy (fun i -> i.Id) |> Seq.toList)
        let indicatorsIndexed = lazy (indicators.Force() |> Seq.map (fun i -> i.Id, i) |> dict)
        let dimensionsIndexed = lazy (dimensions.Force() |> Seq.map (fun i -> i.Id, i) |> dict)
        let indicatorsByTopic = lazy (
            indicators.Force()
            |> Seq.collect (fun i -> i.TopicIds |> Seq.map (fun topicId -> topicId, i.Id))
            |> Seq.groupBy fst
            |> Seq.map (fun (topicId, indicatorIds) -> topicId, indicatorIds |> Seq.map snd |> Seq.cache)
            |> dict)
        let countries = lazy (getCountries [] |> Async.RunSynchronously)
        let dataflows = lazy (getDataflows [] |> Async.RunSynchronously)
        let countriesIndexed = lazy (countries.Force() |> Seq.map (fun c -> c.Id, c) |> dict)
        let dataflowsIndexed = lazy (dataflows.Force() |> Seq.map (fun c -> c.Id, c) |> dict)
        let regions = lazy (getRegions() |> Async.RunSynchronously)
        let regionsIndexed = lazy (regions.Force() |> dict)

        member internal __.Topics = topics.Force()
        member internal __.TopicsIndexed = topicsIndexed.Force()
        member internal __.Indicators = indicators.Force()
        member internal __.IndicatorsIndexed = indicatorsIndexed.Force()
        member internal __.Dimensions = dimensions.Force()
        member internal __.DimensionsByDataflow = dimensions.Force()

        member internal __.DimensionsIndexed = dimensionsIndexed.Force()
        member internal __.DimensionValues = dimensionValues.Force()
        
        member internal __.IndicatorsByTopic = indicatorsByTopic.Force()
        member internal __.Countries = countries.Force()
        member internal __.CountriesIndexed = countriesIndexed.Force()
        member internal __.Dataflows = dataflows.Force()
        member internal __.DataflowsIndexed = dataflowsIndexed.Force()
        member internal __.Regions = regions.Force()
        member internal __.RegionsIndexed = regionsIndexed.Force()
        /// At runtime, download the data
        member internal __.GetDimensionsAsync(agencyId, dataflowId) = 
            async { let! data = getDimensions agencyId dataflowId                        
                    return data }
        member internal x.GetDimensions(agencyId, dataflowId) =
             x.GetDimensionsAsync(agencyId, dataflowId) |> Async.RunSynchronously
        member internal __.GetDataAsync(countryOrRegionCode, indicatorCode) =
            async { let! data =
                      getData
                        [ "countries"
                          countryOrRegionCode
                          "indicators"
                          indicatorCode ]
                        [ "date", "1900:2050" ]
                        "date"
                    return
                      seq { for k, v in data do
                              if not (String.IsNullOrEmpty v) then
                                 yield int k, float v }
                      // It's a time series - sort it :-)  We should probably also interpolate (e.g. see R time series library)
                      |> Seq.sortBy fst }

        member internal x.GetData(countryOrRegionCode, indicatorCode) =
             x.GetDataAsync(countryOrRegionCode, indicatorCode) |> Async.RunSynchronously
        member internal __.GetCountriesInRegion region = getCountries ["region", region] |> Async.RunSynchronously


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
    let dimensionValues = seq { for dimensionValue in connection.DimensionValues -> DimensionValue(connection, dimensionValue.Id) }
    member x.Id = dimensionId
    member x.Name = connection.DimensionsIndexed.[dimensionId].Id
    member x.Description = "Test"
    
    interface IDimensionValue with
        member x.GetDimensionValue(dimensionId) = DimensionValue(connection, dimensionId)
        member x.AsyncGetDimensionValue(dimensionId) = async { return DimensionValue(connection, dimensionId) }
    interface seq<DimensionValue> with member x.GetEnumerator() = dimensionValues.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = dimensionValues.GetEnumerator() :> _

[<DebuggerDisplay("{Name}")>]
[<StructuredFormatDisplay("{Name}")>]
/// Metadata for an Indicator
type IndicatorDescription internal (connection:ServiceConnection, topicCode:string, indicatorCode:string) =
    /// Get the code for the topic of the indicator
    member x.Code = topicCode
    /// Get the code for the indicator
    member x.IndicatorCode = indicatorCode
    /// Get the name of the indicator
    member x.Name = connection.IndicatorsIndexed.[indicatorCode].Name
    /// Get the source of the indicator
    member x.Source = connection.IndicatorsIndexed.[indicatorCode].Source
    /// Get the description of the indicator
    member x.Description = connection.IndicatorsIndexed.[indicatorCode].Description

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
type IIndicatorsDescriptions =
    abstract GetIndicator : indicatorCode:string -> IndicatorDescription

/// [omit]
type IndicatorsDescriptions internal (connection:ServiceConnection, topicCode) =
    let indicatorsDescriptions = seq { for indicatorId in connection.IndicatorsByTopic.[topicCode] -> IndicatorDescription(connection, topicCode, indicatorId) }
    interface IIndicatorsDescriptions with member x.GetIndicator(indicatorCode) = IndicatorDescription(connection, topicCode, indicatorCode)
    interface seq<IndicatorDescription> with member x.GetEnumerator() = indicatorsDescriptions.GetEnumerator()
    interface IEnumerable with member x.GetEnumerator() = indicatorsDescriptions.GetEnumerator() :> _


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
type DataflowCollection<'T when 'T :> Dataflow> internal (connection: ServiceConnection) =
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
        member x.GetDataflows() = DataflowCollection(connection) :> seq<_>
        member x.GetDimensions() = DimensionCollection(connection) :> seq<_>
