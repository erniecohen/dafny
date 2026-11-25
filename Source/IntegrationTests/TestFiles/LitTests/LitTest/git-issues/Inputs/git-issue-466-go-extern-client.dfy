method Main() {
  var n := A.Plain(A.F());
  var x := A.Native.Make();
  var alias := B.Alias.Make();
  var other := C.Get();
  print n, " ", x.Value(), " ", alias.Value(), " ", A.Twice(other, 0), "\n";
}
