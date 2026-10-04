// RUN: %testDafnyForEachCompiler --refresh-exit-code=0 "%s"
// RUN: %testDafnyForEachCompiler --refresh-exit-code=0 "%s" -- --optimize-erasable-datatype-wrapper=false

// Constructor discriminators are properties even when the datatype requires
// a default-value descriptor for a generic parameter.
datatype Maybe<T(0)> = None | Some(value: T)

method Observe<T(0)>() returns (empty: bool)
  ensures empty
{
  var m: Maybe<T> := None;
  empty := m.None?;
}

method Main() {
  var a := Observe<int>();
  var b := Observe<bool>();
  print a, " ", b, "\n";
}
