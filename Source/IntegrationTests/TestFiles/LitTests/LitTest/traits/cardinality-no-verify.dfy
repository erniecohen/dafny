// RUN: %exits-with 2 %build --no-verify -t:lib "%s" --output "%S/Output/cardinality-invalid.doo" > "%t"
// RUN: %exits-with 2 %build --no-verify "%s" --output "%S/Output/cardinality-invalid" >> "%t"
// RUN: %diff "%s.expect" "%t"

trait V {}
datatype D extends V = D(p: V -> bool)
method Main() { }
