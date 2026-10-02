// RUN: %build -t:lib --no-verify "%S/Inputs/cardinality-library.dfy" --output "%S/Output/cardinality-library.doo" > "%t"
// RUN: %exits-with 2 %resolve "%s" --library "%S/Output/cardinality-library.doo" >> "%t"
// RUN: %exits-with 2 %verify --dont-verify-dependencies --filter-symbol=Selected "%s" --library "%S/Output/cardinality-library.doo" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Client {
  import A = Api
  import S = Storage
  datatype D extends A.V = D(b: S.B)
  method Selected() { assert true; }
}
