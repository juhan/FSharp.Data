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
            dataflowsTypeDefinition

        for dataflow in connection.Dataflows do
            let dataflowId, dataflowName, agencyId, version = dataflow.Id, dataflow.Name, dataflow.AgencyID, dataflow.Version
            printfn "dataflowId: %s dataflowName: %s agencyId: %s version: %s" dataflowId, dataflowName, agencyId, version
            let dataflowsTypeDefinition = datafowType dataflowName dataflowId
            //let dataflowsTypeDefinition = ProvidedTypeDefinition(dataflowName, Some typeof<DataObject>, hideObjectMethods = false, nonNullable = true)

            //let mysdmxDataServiceType =
            //    let t = ProvidedTypeDefinition("DatalowsType", Some typeof<SdmxData>, hideObjectMethods = true, nonNullable = true)
            //    for dimension in connection.GetDimensions(agencyId, dataflowId) do
            //        let prop = ProvidedProperty(dimension.DimensionName,
            //                                typeof<string>,
            //                                isStatic=false,
            //                                getterCode = (fun _ -> <@@ dataflowName:string @@>))
            //                                //getterCode = (fun (Singleton arg) -> <@@ ((%%arg : SdmxData) :> ISdmxData).GetTopic(dataflowName) @@>))
            //        t.AddMember prop
            //        serviceTypesType.AddMember t
            //    t

            //dataflowsTypeDefinition.AddMembersDelayed (fun () -> 
            //[ 
            //  yield ProvidedMethod ("MyDataContext", [], mysdmxDataServiceType, isStatic=true,
            //                           invokeCode = (fun _ -> <@@ SdmxData(weEntryPoint) @@>)) 
            //])
  
            // Default Constructior
            //let ctorEmpty = ProvidedConstructor([], invokeCode = fun _ -> <@@  "Hello" :> obj @@>)
            //1. check if DataObject can receive complex arguments like obkects: Answer No no no [Quotations provided by type providers can only contain simple constants.]
             //2. pass connection string so it can create a connection in runtime and fetch dataflow based on index from cache and return an insance of an object with Header Infromation and just dataflw infotmation
            // 3. Is it a problem that for each dataflow new connection will be initialized, at some point it should not be an issue because user will only use 1 or very few dataflows in one context
            //let ctorEmpty = ProvidedConstructor(parameters = [], invokeCode= (fun args -> <@@ DataObject(wsEntryPoint) @@>))

            //resTy.AddMembersDelayed (fun () -> 
            //    [ let urlVal = ""
            //      yield ProvidedMethod ("GetDataContext", [], sdmxDataServiceType, isStatic=true,
            //                               invokeCode = (fun _ -> <@@ DataflowCollection(wsEntryPoint) @@>)) 
            //    ])


            //let ctor = ProvidedConstructor([ for dimension in connection.GetDimensions(agencyId, dataflowId) do
            //                                    yield ProvidedParameter(dimension.Id, typeof<string>)
            //], invokeCode = fun (Singleton arg) -> <@@   
            //    "My internal state" :> obj
            //@@>)
            //dataflowsTypeDefinition.AddMember(ctorEmpty)

            dataflowsTypeDefinition.AddMembersDelayed(
                 fun () ->
                    [ for dimension in connection.GetDimensions(agencyId, dataflowId) do                         
                         if dataflowId = dimension.DataStructureId then
                            let dimensionTypeDefinition = ProvidedTypeDefinition(dimension.DimensionName, Some typeof<DimensionObject>, hideObjectMethods = true, nonNullable = true)
                            let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
                            dimensionTypeDefinition.AddMember ctor
                            let dimensionId = dimension.Id 
                            for dimensionValue in dimension.DimensionValues do
                                let dimensionValueProperty = ProvidedProperty(dimensionValue.Name, typeof<DimensionObject>, isStatic=true, getterCode = fun _ -> <@@ DimensionObject(wsEntryPoint, agencyId, dataflowId, dimensionId) @@>)                                
                                dimensionTypeDefinition.AddMember dimensionValueProperty
                            yield dimensionTypeDefinition])
            serviceTypesType.AddMember dataflowsTypeDefinition
            resTy.AddMember dataflowsTypeDefinition     


        //let sdmxDataServiceType =
        //    let t = ProvidedTypeDefinition("WorldBankDataService", Some typeof<DataflowCollection<Dataflow>>, hideObjectMethods = true, nonNullable = true)
        //    t.AddMembersDelayed (fun () ->
        //        [for dataflow in connection.Dataflows do
        //            yield ProvidedProperty(dataflow.Name,
        //            typeof<Dataflow>,
        //            getterCode = (fun (arg) -> <@@ ((%%arg.[0] : DataflowCollection<Dataflow>) :> IDataflowCollection).GetDataflow(dataflow.Id, dataflow.Name) @@>))
        //        ])
        //    serviceTypesType.AddMember t
        //    t
        //resTy.AddMembersDelayed (fun () -> 
        //    [ let urlVal = ""
        //      yield ProvidedMethod ("GetDataContext", [], sdmxDataServiceType, isStatic=true,
        //                               invokeCode = (fun _ -> <@@ DataflowCollection(wsEntryPoint) @@>)) 
        //    ])
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

