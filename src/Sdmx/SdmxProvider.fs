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
    let defaultWsEntryPoint = "http://api.worldbank.org" //"https://api.worldbank.org/v2/sdmx/rest"
    let cacheDuration = TimeSpan.FromDays 30.0
    let restCache = createInternetFileCache "SdmxSchema" cacheDuration

    let createTypesForWsEntryPoint(sources, worldBankTypeName, asynchronous) = 

        ProviderHelpers.getOrCreateProvidedType cfg this worldBankTypeName <| fun () ->

        // let connection = ServiceConnection(restCache, defaultWsEntryPoint, sources)
 
        let resTy = ProvidedTypeDefinition(asm, ns, worldBankTypeName, None, hideObjectMethods = true, nonNullable = true)

        let serviceTypesType = 
            let t = ProvidedTypeDefinition("ServiceTypes", None, hideObjectMethods = true, nonNullable = true)
            t.AddXmlDoc("<summary>Contains the types that describe the data service</summary>")
            resTy.AddMember t
            t

        let countryType =
            let t = ProvidedTypeDefinition("Country", Some typeof<Country>, hideObjectMethods = true, nonNullable = true)
            // t.AddMembersDelayed (fun () -> 
            //     [ let prop = ProvidedProperty("Indicators", indicatorsType, 
            //                   getterCode = (fun (Singleton arg) -> <@@ ((%%arg : Country) :> ICountry).GetIndicators() @@>))
            //       prop.AddXmlDoc("<summary>The indicators for the country</summary>")
            //       yield prop ] )
            serviceTypesType.AddMember t
            t
        
        let countriesType =
            let countryCollectionType = ProvidedTypeBuilder.MakeGenericType(typedefof<CountryCollection<_>>, [ countryType ])
            let t = ProvidedTypeDefinition("Countries", Some countryCollectionType, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ for country in ["A"; "B"; "C"] do
                    let countryIdVal = "1"
                    let name = country
                    let prop = 
                        ProvidedProperty
                          ( name, countryType, 
                            getterCode = (fun (Singleton arg) -> <@@ ((%%arg : CountryCollection<Country>) :> ICountryCollection).GetCountry(countryIdVal, name) @@>))
                    prop.AddXmlDoc (sprintf "The data for country '%s'" country)
                    yield prop ])
            serviceTypesType.AddMember t
            t

        let sdmxDataServiceType =
            let t = ProvidedTypeDefinition("WorldBankDataService", Some typeof<SdmxData>, hideObjectMethods = true, nonNullable = true)
            t.AddMembersDelayed (fun () -> 
                [ yield ProvidedProperty("Countries", countriesType,  getterCode = (fun (Singleton arg) -> <@@ ((%%arg : SdmxData) :> ISdmxData).GetCountries() @@>)) ])
            serviceTypesType.AddMember t
            t

        resTy.AddMembersDelayed (fun () -> 
            [ let urlVal = sources              
              yield ProvidedMethod ("GetDataContext", [], sdmxDataServiceType, isStatic=true,
                                       invokeCode = (fun _ -> <@@ SdmxData(urlVal) @@>)) 
            ])

        resTy
    
    let paramSdmxType = 
        let sdmxProvTy = ProvidedTypeDefinition(asm, ns, "SdmxDataProvider", None, hideObjectMethods = true, nonNullable = true)
        
        let defaultSourcesStr = ""        
        let helpText = "<summary>.. Sdmx Type Provider .. </summary>
                        <param name='WsEntryPoint'>Sdmx rest web service entry point url</param>
                        <param name='Asynchronous'>Generate asynchronous calls. Defaults to false.</param>"

        sdmxProvTy.AddXmlDoc(helpText)

        let parameters =
            [ ProvidedStaticParameter("WsEntryPoint", typeof<string>, defaultSourcesStr)
              ProvidedStaticParameter("Asynchronous", typeof<bool>, false) ]

        sdmxProvTy.DefineStaticParameters(parameters, fun typeName providerArgs -> 
            let wsEntryPoint = (providerArgs.[0] :?> string)
            let isAsync = providerArgs.[1] :?> bool
            createTypesForWsEntryPoint(wsEntryPoint, typeName, isAsync))
        sdmxProvTy

    // let createStaticProp propertyName = ProvidedProperty(propertyName = propertyName, propertyType = typeof<string>, isStatic = true, getterCode = (fun args -> <@@ "Hello!" @@>))
                              
    // Register the main type with F# compiler
    do this.AddNamespace(ns, [ paramSdmxType ])

