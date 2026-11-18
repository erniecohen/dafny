// Rejected argument-dependent orders: each is reported by the type checker
module RejectedOrders {
  import opened Levels
  import opened ListApi

  // A partial order function, and one that reads the heap: an argument-dependent order is a total arrow
  method Partial(p: bounded<ListObject --> Level, ListObject -> bool>) // error: not total
  method Reads(p: bounded<ListObject ~> Level, ListObject -> bool>) // error: not total
  // The order takes other parameters than the payload
  method Mismatch(p: bounded<int -> Level, ListObject -> bool>) // error: parameters
  // An order whose result is itself argument-dependent
  method Curried(p: bounded<ListObject -> ListObject -> Level, ListObject -> bool>) // error: result is an arrow
}
