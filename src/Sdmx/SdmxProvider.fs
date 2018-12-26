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

    let createTypesForWsEntryPoint(wsEntryPoint, sdmxTypeName, asynchronous) =
        ProviderHelpers.getOrCreateProvidedType cfg this sdmxTypeName <| fun () ->
        let connection = ServiceConnection(restCache, wsEntryPoint)
        let resTy = ProvidedTypeDefinition(asm, ns, sdmxTypeName, Some typeof<obj>, hideObjectMethods = true, nonNullable = true)              

        let serviceTypesType = 
            let t = ProvidedTypeDefinition("ServiceTypes", None, hideObjectMethods = true, nonNullable = true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t
        let datafowType dataflowName dataflowId =
            let dataflowsTypeDefinition = ProvidedTypeDefinition(dataflowName, Some typeof<DataFlowObject>, hideObjectMethods = false, nonNullable = true)
            let ctorEmpty = ProvidedConstructor(parameters = [], invokeCode= (fun args -> <@@ DataFlowObject(wsEntryPoint, dataflowId) @@>))
            dataflowsTypeDefinition.AddMember(ctorEmpty)


            let instanceMeth = 
                ProvidedMethod(methodName = "InstanceMethod", 
                               parameters = [
                                    ProvidedParameter("a",typeof<DimensionObject>)
                                    ProvidedParameter("b",typeof<DimensionObject>)
                                    ProvidedParameter("c",typeof<DimensionObject>)
                               ], 
                               returnType = typeof<DataFlowObject>, 
                               invokeCode = (fun args -> 
                                   <@@
                                   let dims1 = %%args.[1] : DimensionObject
                                   let dims2 = %%args.[2] : DimensionObject
                                   let dims3 = %%args.[3] : DimensionObject
                                   DataFlowObject(wsEntryPoint, dataflowId, [dims1; dims2; dims3])
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
            let dataflowsTypeDefinition = datafowType dataflowName dataflowId
            dataflowsTypeDefinition.AddMembersDelayed(
                 fun () ->
                    [ for dimension in connection.GetDimensions(agencyId, dataflowId) do                         
                         if dataflowId = dimension.DataStructureId then
                            let dimensionTypeDefinition = ProvidedTypeDefinition(dimension.DimensionName, Some typeof<DimensionObject>, hideObjectMethods = true, nonNullable = true)
                            let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
                            dimensionTypeDefinition.AddMember ctor
                            let dimensionId = dimension.Id 
                            for dimensionValue in dimension.DimensionValues do
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

