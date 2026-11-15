// https://github.com/erniecohen/dafny/issues/268
// The provided non-null T and the hidden nullable T? need distinct constructors.
// RUN: %exits-with 4 %baredafny verify --standard-libraries:false --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --standard-libraries:false --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Library {
  export provides T, T.Final
  trait T { function Final(): nat { 0 } }
}
module Client {
  import Library
  function Fixed(t: Library.T): nat { t.Final() }

  lemma CallInLemma(t: Library.T) {
    assert t.Final() >= 0;
  }

  lemma FalseControl(t: Library.T) {
    var n := t.Final();
    assert n >= 0;
    assert false; // Must fail after translating the formerly crashing call.
  }

  lemma BodyStaysHidden(t: Library.T) {
    assert t.Final() == 0; // Must fail: provides does not reveal the body.
  }
}

module RevealedLibrary {
  export reveals T provides T.Final
  trait T { function Final(): nat { 0 } }
}
module RevealedClient {
  import L = RevealedLibrary
  function Fixed(t: L.T): nat { t.Final() }
}

module ClassLibrary {
  export provides T, T.Final
  class T { function Final(): nat { 0 } }
}
module ClassClient {
  import L = ClassLibrary
  function Fixed(t: L.T): nat { t.Final() }
}

module GenericLibrary {
  export provides T, T.Final
  trait T<A> { function Final(a: A): A { a } }
}
module GenericClient {
  import L = GenericLibrary
  function Fixed<A>(t: L.T<A>, a: A): A { t.Final(a) }
}

module MethodLibrary {
  export provides T, T.Final
  trait T {
    method Final() returns (n: nat) { n := 0; }
  }
}
module MethodClient {
  import L = MethodLibrary
  method Call(t: L.T) returns (n: nat) { n := t.Final(); }
}
