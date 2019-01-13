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

    let createTypesForWsEntryPoint(wsEntryPoint, sdmxTypeName, asynchronous, language) =
        ProviderHelpers.getOrCreateProvidedType cfg this sdmxTypeName <| fun () ->
        let connection = ServiceConnection(restCache, wsEntryPoint)
        let resTy = ProvidedTypeDefinition(asm, ns, sdmxTypeName, Some typeof<obj>, hideObjectMethods = true, nonNullable = true)              

        let datafowType dataflowName agencyId dataflowId dataId =
            let dataflowsTypeDefinition = ProvidedTypeDefinition(dataflowName, Some typeof<DataFlowObject>, hideObjectMethods = true, nonNullable = true)
            let dataCtor =
                ProvidedConstructor(
                    parameters = [
                        for dimension in connection.GetDimensions(agencyId, dataflowId) do
                            yield ProvidedParameter(dimension.Name, typeof<DimensionObject>)    
                    ],
                    invokeCode = ( fun args ->
                        let folder = fun state e -> <@@ (%%e:DimensionObject)::%%state @@>
                        let dims = List.fold folder <@@ []:List<DimensionObject> @@> args
                        <@@
                            DataFlowObject(wsEntryPoint, dataId, "", %%dims)
                        @@>
                    )
            )
            let keyCtor = // TODO: implement type checking on the series key passed as argument.
                ProvidedConstructor(
                    parameters = [
                        yield ProvidedParameter("seriesKey", typeof<string>)
                    ],
                    invokeCode = ( fun args ->
                        let seriesKey = <@@ (%%args.Head:string ) @@>
                        <@@
                            DataFlowObject(wsEntryPoint, dataId, %%seriesKey, [])
                        @@>
                    )
            )

            dataflowsTypeDefinition.AddMember(dataCtor)
            dataflowsTypeDefinition.AddMember(keyCtor)
            dataflowsTypeDefinition

        for dataflow in connection.Dataflows do
            let dataflowId, dataflowName, agencyId, version = dataflow.Id, dataflow.Name, dataflow.AgencyID, dataflow.Version
            let dataflowsTypeDefinition = datafowType dataflowName agencyId dataflowId dataflow.DataId
            dataflowsTypeDefinition.AddMembersDelayed(
                 fun () ->
                    [ for dimension in connection.GetDimensions(agencyId, dataflowId) do                         
                         if dataflowId = dimension.DataStructureId then
                            let dimensionTypeDefinition =
                                ProvidedTypeDefinition(dimension.Name,
                                    Some typeof<DimensionObject>,
                                    hideObjectMethods = true, nonNullable = true)
                            let dimensionId = dimension.Id 
                            for dimensionValue in dimension.Values do
                                let dimensionValueId = dimensionValue.Id
                                let dimensionValueProperty =
                                    ProvidedProperty(dimensionValue.Name,
                                        typeof<DimensionObject>,
                                        isStatic=true,
                                        getterCode = fun _ ->
                                            <@@
                                                DimensionObject(wsEntryPoint, agencyId, dataflowId, dimensionId, dimensionValueId)
                                            @@>
                                    )
                                dimensionTypeDefinition.AddMember dimensionValueProperty
                            dimensionTypeDefinition.AddXmlDoc(dimension.Description)
                            yield dimensionTypeDefinition
                    ]
                )
            resTy.AddMember dataflowsTypeDefinition     
        resTy

    let paramSdmxType =
        let sdmxProvTy = ProvidedTypeDefinition(asm, ns, "SdmxDataProvider", None, hideObjectMethods = true, nonNullable = true)

        let defaultWsEntryPoint = ""//"https://api.worldbank.org/v2/sdmx/rest"

        let helpText = "<summary> Sdmx Type Provider </summary>
                        <param name='WsEntryPoint'>Sdmx rest web service entry point url.</param>
                        <param name='Asynchronous'>Generate asynchronous calls. Defaults to false.</param>
                        <param name='Language'>Language of type names, information and text. Defaults to en.</param>
                        "

        sdmxProvTy.AddXmlDoc(helpText)

        let parameters =
            [ ProvidedStaticParameter("WsEntryPoint", typeof<string>, defaultWsEntryPoint)
              ProvidedStaticParameter("Asynchronous", typeof<bool>, false)
              ProvidedStaticParameter("Language", typeof<string>, "en")]

        sdmxProvTy.DefineStaticParameters(parameters, fun typeName providerArgs ->
            let wsEntryPoint = providerArgs.[0] :?> string
            let isAsync = providerArgs.[1] :?> bool
            let language = providerArgs.[2] :?> string
            createTypesForWsEntryPoint(wsEntryPoint, typeName, isAsync, language))
        sdmxProvTy

    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ paramSdmxType ])

