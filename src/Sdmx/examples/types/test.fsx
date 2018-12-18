#r @"/Users/dn/RiderProjects/Library/Library/bin/Debug/library.dll"
open Library.TypeProviders

type sdmx = SdmxDataProvider<WsEntryPoint="">
let dataflows = sdmx.GetDataFlowsContext()

let a = dataflows.A.
let b = dataflows.B
let c = dataflows.C









// SdmxDataProvider.
// type Type1 = Library.TypeProviders.Type1
// Type1("")
type MyNestedType = Type1