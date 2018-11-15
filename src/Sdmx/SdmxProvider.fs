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
        ProviderHelpers.getOrCreateProvidedType cfg this sdmxTypeName <| fun () ->
        let connection = ServiceConnection(restCache, weEntryPoint)
        let resTy = ProvidedTypeDefinition(asm, ns, sdmxTypeName, None, hideObjectMethods = true, nonNullable = true)
        
        let serviceTypesType =
            let t = ProvidedTypeDefinition("ServiceTypes", None, hideObjectMethods = true, nonNullable = true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t
        
        let rec dimensionValueType =
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
            t 
        
        and dimensionType = 
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
            t        

        let dimensionsType =
            let dimensionCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<DimensionCollection<_>>, [ dimensionType ])
            let t = ProvidedTypeDefinition("Dimensions", Some dimensionCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for dimension in connection.Dimensions do
                      let enumerationId = dimension.EnumerationId
                      let prop = 
                          ProvidedProperty
                            ( dimension.Id, dimensionType, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : DimensionCollection<Dimension>) :> IDimensionCollection).GetDimension(dimension.Id) @@>))
                      if not (String.IsNullOrEmpty dimension.Position) then prop.AddXmlDoc(dimension.Position)
                      yield prop ])
            serviceTypesType.AddMember t
            t
        
        let dataflowType =
            let t = ProvidedTypeDefinition("Dataflow", Some typeof<Dimension>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () ->
                [ let prop = ProvidedProperty("Dimensions", dimensionsType,
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Dataflow) :> IDataflow).GetDimensions() @@>))
                  prop.AddXmlDoc("<summary>The dimensions for the dataflow</summary>")
                  yield prop ])
            serviceTypesType.AddMember t
            t

        let dataflowsType =
            let dataflowCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<DataflowCollection<_>>, [ dimensionsType ])
            let t = ProvidedTypeDefinition("Dataflows", Some dataflowCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () ->
                [ for dataflow in connection.Dataflows do
                    let prop =
                        ProvidedProperty
                          ( dataflow.Name, dimensionsType,
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : DataflowCollection<Dataflow>) :> IDataflowCollection).GetDataflow(dataflow.Id, dataflow.Name) @@>))
                    prop.AddXmlDoc (sprintf "The data for dataflow '%s'" dataflow.Name)                    
                    yield prop])
            serviceTypesType.AddMember t
            t

        let sdmxDataServiceType =
            let t = ProvidedTypeDefinition("SdmxDataService", Some typeof<SdmxData>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () ->
                [ yield ProvidedProperty("Dataflows", dataflowsType,  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : SdmxData) :> ISdmxData).GetDataflows() @@>)) ])
            serviceTypesType.AddMember t
            t

        resTy.AddMembersDelayed (fun () ->
            [ 
                yield ProvidedMethod ("GetDataContext", [], sdmxDataServiceType, isStatic=true,invokeCode = (fun _ -> <@@ SdmxData(weEntryPoint) @@>));
                yield ProvidedMethod ("GetDataflowContext", [], dataflowsType, isStatic=true,invokeCode = (fun _ -> <@@ weEntryPoint @@>))
            ]
        )
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
            let wsEntryPoint = providerArgs.[0] :?> string
            let isAsync = providerArgs.[1] :?> bool
            createTypesForWsEntryPoint(wsEntryPoint, typeName, isAsync))
        sdmxProvTy

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ paramSdmxType ])

