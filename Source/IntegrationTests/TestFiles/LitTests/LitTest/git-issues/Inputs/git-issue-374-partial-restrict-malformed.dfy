// Each malformed declaration must be rejected before intrinsic facts are emitted.
module Bad {
  ghost function {:transparent} {:functionDomain "partialRestrict"} NoBody<A, B>(f: A --> B): A --> B 
  ghost function {:transparent} {:functionDomain "partialRestrict"} Total<A, B>(f: A -> B): A -> B { (x: A) => f(x) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} HeapReading<A, B>(f: A ~> B): A ~> B { (x: A) requires f.requires(x) reads f.reads(x) => f(x) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} MissingPre<A, B>(f: A --> B): A --> B { (x: A) => f(x) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} WrongPre<A, B>(f: A --> B): A --> B { (x: A) requires false => f(x) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} WrongBody<A, B>(f: A --> B): A --> B { (x: A) requires f.requires(x) => if true then f(x) else f(x) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} WrongArgument<A, B>(f: A --> B): A --> B { (x: A) requires f.requires(x) => f(var y := x; y) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} WrongResult<A, B>(f: A --> B): A -> B { (x: A) requires f.requires(x) => f(x) }
  ghost function {:transparent} {:functionDomain "partialRestrict"} Specification<A, B>(f: A --> B): A --> B
 requires true { (x: A) requires f.requires(x) => f(x) }
}
