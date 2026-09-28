// RUN: %exits-with 2 %resolve --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module A { type T(==,!new,00) }
module B { type T(==,!new,00) }

module C {
  import opened A
  datatype D = D(f: T -> bool)
  datatype Partial = Partial(f: T --> bool)
  datatype General = General(f: T ~> bool)
  datatype Nested = Nested(f: (T -> bool) -> bool)
}

module E {
  import opened C
  import opened B
  function G(): D { D((x: T) => true) }
  function P(f: T --> bool): Partial { Partial(f) }
  function H(f: T ~> bool): General { General(f) }
  function N(f: (T -> bool) -> bool): Nested { Nested(f) }
}
