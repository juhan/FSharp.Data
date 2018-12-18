type CustomerName internal (firstName, middleInitial, lastName) = 

    let dataDict = lazy (dict List.seq(1, 1.0))
    member this.FirstName = firstName
    member x.MiddleInitial = middleInitial
    member dato.LastName = lastName  


let a = CustomerName("Demo", "Tall", "Nodia")

a.FirstName
a.MiddleInitial
a.LastName