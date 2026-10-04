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
