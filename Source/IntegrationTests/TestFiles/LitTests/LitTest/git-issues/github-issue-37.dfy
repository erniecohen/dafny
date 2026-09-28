// RUN: %exits-with 2 %resolve --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module A { type T(==,!new,00) }
module B { type T(==,!new,00) }

module C {
  import opened A
  datatype D = D(f: T -> bool)
}

module E {
  import opened C
  import opened B
  function G(): D { D((x: T) => true) }
}
