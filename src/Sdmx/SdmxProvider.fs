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
        
        for dataflow in connection.Dataflows do
            let dataflowId, dataflowName, agencyId, version = dataflow.Id, dataflow.Name, dataflow.AgencyID, dataflow.Version
            printfn "dataflowId: %s dataflowName: %s agencyId: %s version: %s" dataflowId, dataflowName, agencyId, version
            let dataflowsTypeDefinition = ProvidedTypeDefinition(dataflowName, None, hideObjectMethods = true, nonNullable = true)
  
            // Default Constructior  
            let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
            dataflowsTypeDefinition.AddMember(ctor)

            // // Default property
            let innerState = ProvidedProperty("InnerState", typeof<string>, isStatic=true, getterCode = fun args -> <@@ "Hello":string @@>)
            dataflowsTypeDefinition.AddMember(innerState)
            
            dataflowsTypeDefinition.AddMembersDelayed(
                 fun () -> [
                     for dimension in connection.GetDimensions(agencyId, dataflowId) do
                         if dataflowId = dimension.DataStructureId then
                             let dimensionTypeDefinition = ProvidedTypeDefinition(dimension.DimensionName, Some typeof<obj>, hideObjectMethods = true, nonNullable = true)
                             let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
                             dimensionTypeDefinition.AddMember ctor
                             dataflowsTypeDefinition.AddMember(dimensionTypeDefinition)
                             for dimensionValue in dimension.DimensionValues do
                                 // codeId, codeName 
                                 let dimensionValueProperty = ProvidedProperty(dimensionValue.Name, typeof<string>, isStatic=true, getterCode = fun _ -> <@@ dimensionValue.Id:string @@>)
                                 dimensionTypeDefinition.AddMember(dimensionValueProperty)
                 ]
             )
            
            //for dimension in connection.GetDimensions(agencyId, dataflowId) do
            //    printfn "%s - %s - %s - %s " dataflowId dimension.Id dimension.AgencyId dimension.DataStructureId
            //    if dataflowId = dimension.DataStructureId then
            //        let dimensionTypeDefinition = ProvidedTypeDefinition(dimension.DimensionName, Some typeof<obj>)
            //        let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
            //        dimensionTypeDefinition.AddMember(ctor)
            //        dataflowsTypeDefinition.AddMember(dimensionTypeDefinition)
            //        for dimensionValue in dimension.DimensionValues do
            //            // codeId, codeName 
            //            let dimensionValueProperty = ProvidedProperty(dimensionValue.Id, typeof<string>, isStatic=true, getterCode = fun _ -> <@@ dimensionValue.Id:string @@>)
            //            dimensionTypeDefinition.AddMember(dimensionValueProperty)
                    
            resTy.AddMember dataflowsTypeDefinition     
              
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

