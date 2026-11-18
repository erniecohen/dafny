// RUN: %exits-with 2 %resolve --referrers --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: in the arguments of a call, M`x names the memory location of the
// in-parameter x of the method M that is called (under --referrers).  A binding of a lemma's results is a call of
// the lemma only in its arguments: in the body of the binding, the lemma is not being called any more.

lemma L(n: int) returns (m: int)

method M() {
  ghost var a := (var m := L(0); var location := L`n; m);  // error: L is not being called here
}
