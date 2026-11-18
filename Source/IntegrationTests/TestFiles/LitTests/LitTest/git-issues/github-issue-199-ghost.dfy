// RUN: %exits-with 2 %resolve --lemma-bindings "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: the variables that a binding of a lemma's results declares
// are ghost, with or without the keyword "ghost".  A compiled expression may bind them and use them in ghost
// positions (see github-issue-199-compile.dfy), but its value may not depend on them.

lemma Find(n: nat) returns (m: nat)
  ensures m > n
{
  m := n + 1;
}

datatype D = D(x: nat, ghost y: nat)

function InAFunction(n: nat): nat {
  var m := Find(n);
  m  // error: a ghost variable in a compiled expression
}

function WithGhostKeyword(n: nat): nat {
  ghost var m := Find(n);
  m  // error: a ghost variable in a compiled expression
}

method InAMethod(n: nat) returns (r: nat) {
  r := var m := Find(n); m;  // error: a ghost variable in a compiled expression
}

function InACompiledField(n: nat): D {
  var m := Find(n);
  D(m, n)  // error: a ghost variable in a compiled expression
}

function InAGhostField(n: nat): D {
  var m := Find(n);
  D(n, m)
}

function InAnAssertion(n: nat): nat {
  var m := Find(n);
  assert m > n;
  n
}

method InAGhostVariable(n: nat) returns (r: nat) {
  ghost var g := var m := Find(n); m;
  r := n;
}
