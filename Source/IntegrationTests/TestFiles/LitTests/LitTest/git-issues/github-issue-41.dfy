// RUN: %baredafny resolve --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny resolve --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Color = Red | Blue
class C { constructor Init(x: int) {} }
method Test(color: Color) {
  match color
  case Red => var v := new C.Init(165);
  case Blue =>
}
class Anonymous { constructor(x: int) {} }
class Generic<T> { constructor Init(x: T) {} }
method Controls<T>(color: Color, x: T) {
  var named := new C.Init(165);
  var anonymous := new Anonymous(165);
  var generic := new Generic<T>.Init(x);
  match color
  case Red => var a := new Anonymous(165);
  case Blue => var g := new Generic<T>.Init(x);
}
