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
        let dataflowsType =
            let dataflowsType = ProvidedTypeDefinition("DatalowsType", None, hideObjectMethods = true, nonNullable = true)
            for dataflow in connection.Dataflows do
                let dataflowId, dataflowName, agencyId, version = dataflow.Id, dataflow.Name, dataflow.AgencyID, dataflow.Version
                printfn "dataflowId: %s dataflowName: %s agencyId: %s version: %s" dataflowId, dataflowName, agencyId, version
                let dataflowsTypeDefinition = ProvidedTypeDefinition(dataflowName, Some typeof<Dataflow>, hideObjectMethods = true, nonNullable = true)
  
                // Default Constructior  
                let ctor = ProvidedConstructor([ for dimension in connection.GetDimensions(agencyId, dataflowId) do
                                                    yield ProvidedParameter(dimension.Id, typeof<string>)
                ], invokeCode = fun (Singleton arg) -> <@@   
                    printfn "Constr:  %s" %%arg
                    "My internal state" :> obj
                @@>)
                dataflowsTypeDefinition.AddMember(ctor)

                // // Default property
                let innerState = ProvidedProperty("InnerState", typeof<string>, isStatic=false, getterCode = fun args -> <@@ "Hello":string @@>)
                dataflowsTypeDefinition.AddMember(innerState)

                dataflowsTypeDefinition.AddMembersDelayed(
                     fun () ->
                        [ for dimension in connection.GetDimensions(agencyId, dataflowId) do
                             if dataflowId = dimension.DataStructureId then
                                let dimensionTypeDefinition = ProvidedTypeDefinition(dimension.DimensionName, Some typeof<DimensionValueRecord>, hideObjectMethods = true, nonNullable = true)
                                let ctor = ProvidedConstructor([], invokeCode = fun _ -> <@@ "My internal state" :> obj @@>)
                                dimensionTypeDefinition.AddMember ctor
                                for dimensionValue in dimension.DimensionValues do
                                    let quoteVale = [dimensionValue.Id; dimensionValue.Name] |> String.concat "~"
                                    let dimenValueType = {Id=dimensionValue.Id; Name=dimensionValue.Name}
                                    let dimensionValueProperty = ProvidedProperty(dimensionValue.Name,
                                        typeof<Demo>,
                                        isStatic=false,
                                        getterCode = (fun (arg) -> <@@ ((%%(arg.[0]) : TopicCollection<Demo>) :> ITopicCollection).GetTopic("l") @@>))
                                    //let dimensionValueTypeDefinition = ProvidedTypeDefinition(dimensionValue.Name, Some typeof<obj>, hideObjectMethods = true, nonNullable = true)
                                    //let ctor2 = ProvidedConstructor([], invokeCode = fun _ -> <@@ "Hello " :> obj @@>)                            
                                    //dimensionValueTypeDefinition.AddMember dimensionValueProperty
                                    //dimensionValueTypeDefinition.AddMember ctor2
                                    dimensionTypeDefinition.AddMember dimensionValueProperty
                                yield dimensionTypeDefinition])
                dataflowsType.AddMember dataflowsTypeDefinition
                



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
                    
            resTy.AddMember dataflowsType
            dataflowsType
              
        let mysdmxDataServiceTypeOld =
            // check why property is not generated after .()
            let t = ProvidedTypeDefinition("MySdmxDataServiceType", Some typeof<SdmxData>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [
                    yield ProvidedProperty("MyTypeAsWell",
                                    typeof<Demo>,
                                    isStatic=false,
                                    getterCode = (fun (Singleton arg) -> <@@ ((%%arg : SdmxData) :> ISdmxData).GetTopic("toopic") @@>))
                ])
            serviceTypesType.AddMember t
            t

        let dimensionsType =
            let t = ProvidedTypeDefinition("Indicators", Some typeof<Indicators>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for indicator in connection.Indicators do
                      let indicatorIdVal = indicator.Id
                      let prop = 
                        if asynchronous then 
                          ProvidedProperty
                            ( indicator.Name, typeof<Async<Indicator>> , 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Indicators) :> IIndicators).AsyncGetIndicator(indicatorIdVal) @@>))
                        else
                          ProvidedProperty
                            ( indicator.Name, typeof<Indicator>, 
                              getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Indicators) :> IIndicators).GetIndicator(indicatorIdVal) @@>))

                      if not (String.IsNullOrEmpty indicator.Description) then prop.AddXmlDoc(indicator.Description)
                      yield prop ] )
            serviceTypesType.AddMember t
            t


        let mysdmxDataServiceType =
            let t = ProvidedTypeDefinition("DatalowsType", Some typeof<SdmxData>, hideObjectMethods = true, nonNullable = true)
            for dataflow in connection.Dataflows do
                let dataflowName = dataflow.Name
                let prop = ProvidedProperty(dataflow.Name,
                                        typeof<Demo>,
                                        isStatic=false,
                                        getterCode = (fun (Singleton arg) -> <@@ ((%%arg : SdmxData) :> ISdmxData).GetTopic(dataflowName) @@>))

                let ccc = ProvidedProperty("CCC", typeof<string>, isStatic=false, getterCode = (fun _ -> <@@ "demo":string @@>))
                
                t.AddMember prop
            serviceTypesType.AddMember t
            t

        resTy.AddMembersDelayed (fun () -> 
            [ 
              yield ProvidedMethod ("MyDataContext", [], mysdmxDataServiceType, isStatic=true,
                                       invokeCode = (fun _ -> <@@ SdmxData("sasaas") @@>)) 
            ])

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

