// Newtypes of https://github.com/erniecohen/dafny/issues/479, which need the refreshed type system
module Newtype {
  datatype W = W(f: () ~> bool)
  newtype NS = s: seq<W> | true
  newtype NG<X> = s: seq<X> | true
  newtype NI = i: int | true
  class Ref { ghost var a: NS  ghost var b: NG<W>  ghost var c: NI  ghost var d: NG<int> }
  // The first two stores are rejected
  method M(r: Ref, a: NS, b: NG<W>, d: NG<int>) modifies r { r.a := a; r.b := b; r.c := 0; r.d := d; }
}
