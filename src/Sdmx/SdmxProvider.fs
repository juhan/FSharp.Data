// --------------------------------------------------------------------------------------
// The SDMX type provider
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open System.Net
open System.Xml.Linq
open FSharp.Core.CompilerServices
open FSharp.Quotations
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.Caching
open FSharp.Data.Runtime.Sdmx

// ----------------------------------------------------------------------------------------------

[<TypeProvider>]
type public SmdxProvider(cfg:TypeProviderConfig) as this =
    inherit DisposableTypeProviderForNamespaces(cfg, assemblyReplacementMap=[ "FSharp.Data.DesignTime", "FSharp.Data" ])

    // Generate namespace and type 'FSharp.Data.SmdxProvider'
    let asm = AssemblyResolver.init cfg (this :> TypeProviderForNamespaces)
    let ns = "FSharp.Data"

    let cacheDuration = TimeSpan.FromDays 30.0
    let restCache = createInternetFileCache "SdmxSchema" cacheDuration

    let createTypesForWsEntryPoint(weEntryPoint, sdmxTypeName, asynchronous) =
        // printfn "[createTypesForWsEntryPoint]-->[weEntryPoint]-------- %s --------" weEntryPoint
        // printfn "createTypesForWsEntryPoint>weEntryPoint-------- %s --------" weEntryPoint
        // printfn "createTypesForWsEntryPoint>sdmxTypeName-------- %s --------" sdmxTypeName

        ProviderHelpers.getOrCreateProvidedType cfg this sdmxTypeName <| fun () ->

        let connection = ServiceConnection(restCache, weEntryPoint)
        printfn "[SDMX]-createTypesForWsEntryPoint-->[weEntryPoint]-------- %s --------" weEntryPoint

        let resTy = ProvidedTypeDefinition(asm, ns, sdmxTypeName, None, hideObjectMethods = true, nonNullable = true)
        
        let serviceTypesType =
            printfn "<1 [serviceTypesType] >" 
            let t = ProvidedTypeDefinition("ServiceTypes", None, hideObjectMethods = true, nonNullable = true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            printfn "<1 [serviceTypesType] return >" 
            t
        
        let rec dimensionValueType =
            printfn "<2 [dimensionValueType] >" 
            let t = ProvidedTypeDefinition("DimensionValue", Some typeof<DimensionValue>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for dimension in connection.Dimensions do
                      let enumerationId = dimension.EnumerationId
                      let prop = 
                          ProvidedProperty
                            ( dimension.Id, dimensionType, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dimensions) :> IDimensions).GetDimension(enumerationId) @@>))

                      if not (String.IsNullOrEmpty dimension.Position) then prop.AddXmlDoc(dimension.Position)
                      yield prop                     
                      ])
            serviceTypesType.AddMember t
            printfn "<2 [dimensionValueType] return >" 
            t 
        
        and dimensionType = 
            printfn "<3 [dimensionType] >" 
            let t = ProvidedTypeDefinition("Dimension", Some typeof<Dimension>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for dimensionValue in connection.DimensionValues do
                    let prop = 
                          ProvidedProperty
                            ( dimensionValue.Name, dimensionValueType, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dimensions) :> IDimensions).GetDimension(dimensionValue.Id) @@>))
                    if not (String.IsNullOrEmpty dimensionValue.Name) then prop.AddXmlDoc(dimensionValue.Name)
                    yield prop ])
            serviceTypesType.AddMember t
            printfn "<3 [dimensionType] return >" 
            t        

        let dimensionsType agencyId =
            printfn "< [dimensionsType] %s >" agencyId
            let localAgencyId = agencyId
            printfn "< [dimensionsType]-[Local] %s >" localAgencyId
            let t = ProvidedTypeDefinition("Dimensions", Some typeof<Dimensions>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for dimension in connection.Dimensions do
                      printfn "dimension.agencyId [%s] - [%s] - [ %s ]" dimension.AgencyId agencyId localAgencyId
                      if dimension.AgencyId = agencyId then
                          let enumerationId = dimension.EnumerationId
                          let prop = 
                              ProvidedProperty
                                ( dimension.Id, dimensionType, 
                                  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dimensions) :> IDimensions).GetDimension(enumerationId) @@>))

                          if not (String.IsNullOrEmpty dimension.Position) then prop.AddXmlDoc(dimension.Position)
                          yield prop                     
                ] @ [ProvidedProperty
                            ( "agencyId", dimensionType, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dimensions) :> IDimensions).GetDimension("") @@>))])
            serviceTypesType.AddMember t
            printfn "< [dimensionsType] return %s >" agencyId
            t
        
        // let dataflowType agencyId =
        //     printfn "<5 [dataflowType] %s >" agencyId
        //     let t = ProvidedTypeDefinition("Dataflow", Some typeof<Dataflow>, hideObjectMethods = true, nonNullable = true)
        //     t.AddMembersDelayed (fun () ->
        //         [ let prop = ProvidedProperty("Dimensions", dimensionsType agencyId,
        //                       getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dataflow) :> IDataflow).GetDimensions() @@>))
        //           prop.AddXmlDoc("<summary>The dimensions for the dataflow</summary>")
        //           yield prop ] @ [
        //               ProvidedProperty(agencyId, dimensionsType agencyId,
        //                       getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dataflow) :> IDataflow).GetDimensions() @@>));
        //               ProvidedProperty("agencyId", dimensionsType agencyId,
        //                       getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dataflow) :> IDataflow).GetDimensions() @@>))
        //           ] )
        //     serviceTypesType.AddMember t
        //     printfn "<5 [dataflowType] return %s >" agencyId
        //     t

        let dataflowsType =
            printfn "<4 [dataflowsType] >"
            let dataflowCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<DataflowCollection<_>>, [ dimensionsType "" ])
            let t = ProvidedTypeDefinition("Dataflows", Some dataflowCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () ->
                [ for dataflow in connection.Dataflows do
                    let prop =
                        ProvidedProperty
                          ( dataflow.Name, dimensionsType dataflow.AgencyID,
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : DataflowCollection<Dataflow>) :> IDataflowCollection).GetDataflow(dataflow.Id, dataflow.Name) @@>))
                    prop.AddXmlDoc (sprintf "The data for dataflow '%s'" dataflow.Name)
                    let prop2 =
                        ProvidedProperty
                          ( dataflow.AgencyID, dimensionsType dataflow.AgencyID,
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : DataflowCollection<Dataflow>) :> IDataflowCollection).GetDataflow(dataflow.Id, dataflow.Name) @@>))
                    yield prop; yield prop2 ])
            serviceTypesType.AddMember t
            printfn "<4 [dataflowsType] return >"
            t

        let sdmxDataServiceType =
            printfn "<6 [sdmxDataServiceType] >"
            let t = ProvidedTypeDefinition("SdmxDataService", Some typeof<SdmxData>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () ->
                [ 
                    yield ProvidedProperty("Dataflows", dataflowsType,  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : SdmxData) :> ISdmxData).GetDataflows() @@>))
                ])
            serviceTypesType.AddMember t
            printfn "<6 [sdmxDataServiceType] retryb >"
            t
        
        printfn "Add [GetDataContext] DelayedMemeber"

        resTy.AddMembersDelayed (fun () ->
            printfn "<7 [AddMembersDelayed] retryb >"
            [ let urlVal = weEntryPoint
              yield ProvidedMethod ("GetDataContext", [], sdmxDataServiceType, isStatic=true,invokeCode = (fun _ -> <@@ SdmxData(urlVal) @@>))
            ]
        )
        printfn "<7 [AddMembersDelayed] retryb return >"
        resTy

    let paramSdmxType =
        let sdmxProvTy = ProvidedTypeDefinition(asm, ns, "SdmxDataProvider", None, hideObjectMethods = true, nonNullable = true)

        let defaultWsEntryPoint = ""//"https://api.worldbank.org/v2/sdmx/rest"

        let helpText = "<summary>.. Sdmx Type Provider .. </summary>
                        <param name='WsEntryPoint'>Sdmx rest web service entry point url.</param>
                        <param name='Asynchronous'>Generate asynchronous calls. Defaults to false.</param>"

        sdmxProvTy.AddXmlDoc(helpText)

        let parameters =
            [ ProvidedStaticParameter("WsEntryPoint", typeof<string>, defaultWsEntryPoint)
              ProvidedStaticParameter("Asynchronous", typeof<bool>, false) ]

        sdmxProvTy.DefineStaticParameters(parameters, fun typeName providerArgs ->
            
            // printfn "paramSdmxType>typeName-------- %s --------" typeName
            // paramWbType>typeName-------- WorldBankDataProvider,Sources="World Development Indicators",Asynchronous="True" --------
            // paramSdmxType>typeName-------- SdmxDataProvider,WsEntryPoint="https://api.worldbank.org/v2/sdmx/rest" --------

            let wsEntryPoint = providerArgs.[0] :?> string
            let isAsync = providerArgs.[1] :?> bool
            
            // printfn "paramSdmxType>wsEntryPoint-------- %s --------" wsEntryPoint
            // printfn "paramSdmxType>isAsync-------- %b --------" isAsync

            createTypesForWsEntryPoint(wsEntryPoint, typeName, isAsync))
        sdmxProvTy

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ paramSdmxType ])

