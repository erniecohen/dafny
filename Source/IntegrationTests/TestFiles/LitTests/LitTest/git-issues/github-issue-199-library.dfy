// RUN: echo "a library that binds the results of lemmas builds" > "%t"
// RUN: %baredafny build %args -t:lib --lemma-bindings --output "%t.lib.doo" "%S/Inputs/github-issue-199-lib.dfy" >> "%t"
// RUN: echo "a client with --lemma-bindings" >> "%t"
// RUN: %exits-with 4 %baredafny verify %args --lemma-bindings --library "%t.lib.doo" "%s" >> "%t"
// RUN: echo "a client without --lemma-bindings" >> "%t"
// RUN: %exits-with 1 %baredafny verify %args --library "%t.lib.doo" "%s" >> "%t"
// RUN: echo "a library built without --lemma-bindings, and a client that binds the results of its lemma" >> "%t"
// RUN: %baredafny build %args -t:lib --output "%t.plain.doo" "%S/Inputs/github-issue-199-lib-plain.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify %args --lemma-bindings --library "%t.plain.doo" "%S/Inputs/github-issue-199-client-plain.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/199: a function of a library may bind the results of a lemma.  A
// client knows about the bound results what the lemma's postcondition says, where it can see the function's
// body, and its own binding of the same lemma at the same arguments gets the same results.  A library that was
// built with --lemma-bindings is for clients that use the option too.  A client may bind the results of a
// lemma from a library that was built without the option.

module Client {
  import Lib

  lemma Use(n: nat)
    ensures Lib.Above(n) > n
  {
  }

  lemma NotTheBody(n: nat)
    ensures Lib.Above(n) == n + 1  // error: only the postcondition of Find is known
  {
  }

  ghost function Mine(n: nat): nat {
    var m := Lib.Find(n); m
  }

  lemma SameResults(n: nat)
    ensures Mine(n) == Lib.Above(n)
  {
  }

  lemma NothingAboutByPrivate(n: nat)
    ensures Lib.ByPrivate(n) > n  // error: the body of ByPrivate is not exported
  {
  }
}

module ClientOfProvided {
  import Lib`Provided

  lemma Use(n: nat)
    ensures Lib.Above(n) > n  // error: the body of Above is not exported
  {
  }
}

module ClientOfConcrete {
  import Concrete

  lemma Use(n: nat)
    ensures Concrete.Above(n) == n + 1
  {
  }
}
