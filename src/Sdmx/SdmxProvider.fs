// --------------------------------------------------------------------------------------
// The SDMX type provider
// --------------------------------------------------------------------------------------

namespace ProviderImplementation

open System
open FSharp.Core.CompilerServices
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

    let createTypesForWsEntryPoint(wsEntryPoint, sdmxTypeName, asynchronous) =
        ProviderHelpers.getOrCreateProvidedType cfg this sdmxTypeName <| fun () ->
        let connection = ServiceConnection(restCache, wsEntryPoint)
        let resTy = ProvidedTypeDefinition(asm, ns, sdmxTypeName, Some typeof<obj>, hideObjectMethods = true, nonNullable = true)              

        let serviceTypesType = 
            let t = ProvidedTypeDefinition("ServiceTypes", None, hideObjectMethods = true, nonNullable = true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t
        let datafowType dataflowName agencyId dataflowId dataId =
            let dataflowsTypeDefinition = ProvidedTypeDefinition(dataflowName, Some typeof<DataFlowObject>, hideObjectMethods = false, nonNullable = true)
            let ctorEmpty = ProvidedConstructor(parameters = [], invokeCode= (fun _ -> <@@ DataFlowObject(wsEntryPoint, dataflowId) @@>))
            dataflowsTypeDefinition.AddMember(ctorEmpty)
            let instanceMeth = 
                ProvidedMethod(methodName = "FetchData", 
                               parameters = [
                                    for dimension in connection.GetDimensions(agencyId, dataflowId) do
                                        yield ProvidedParameter(dimension.Name, typeof<DimensionObject>)                                        
                               ], 
                               returnType = typeof<DataFlowObject>, 
                               invokeCode = (fun args ->
                                   let dims = List.fold ( fun state e -> <@@ (%%e:DimensionObject)::%%state @@>) <@@ []:List<DimensionObject> @@> args.Tail
                                   <@@
                                       DataFlowObject(wsEntryPoint, dataId, %%dims)
                                   @@>
                                   )
                               )

            instanceMeth.AddXmlDocDelayed(fun () -> "This is an instance method")
            // Add the instance method to the type.
            dataflowsTypeDefinition.AddMember instanceMeth

            dataflowsTypeDefinition

        for dataflow in connection.Dataflows do
            let dataflowId, dataflowName, agencyId, version = dataflow.Id, dataflow.Name, dataflow.AgencyID, dataflow.Version
            //printfn "dataflowId: %s dataflowName: %s agencyId: %s version: %s" dataflowId, dataflowName, agencyId, version
            let dataflowsTypeDefinition = datafowType dataflowName agencyId dataflowId dataflow.DataId
            dataflowsTypeDefinition.AddMembersDelayed(
                 fun () ->
                    [ for dimension in connection.GetDimensions(agencyId, dataflowId) do                         
                         if dataflowId = dimension.DataStructureId then
                            let dimensionTypeDefinition = ProvidedTypeDefinition(dimension.Name, Some typeof<DimensionObject>, hideObjectMethods = true, nonNullable = true)
                            let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
                            dimensionTypeDefinition.AddMember ctor
                            let dimensionId = dimension.Id 
                            for dimensionValue in dimension.Values do
                                let dimensionValueId = dimensionValue.Id
                                let dimensionValueProperty = ProvidedProperty(dimensionValue.Name, typeof<DimensionObject>, isStatic=true, getterCode = fun _ -> <@@ DimensionObject(wsEntryPoint, agencyId, dataflowId, dimensionId, dimensionValueId) @@>)                                
                                dimensionTypeDefinition.AddMember dimensionValueProperty
                            yield dimensionTypeDefinition])
            serviceTypesType.AddMember dataflowsTypeDefinition
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

