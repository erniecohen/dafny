// Gate (a) of https://github.com/erniecohen/dafny/issues/211: a library that is verified alone.  Its clients
// try to invalidate it by extending its traits.
module Lib {
  // Open: other modules may implement F, and every implementation is held to this bound.
  trait {:termination false} T {
    function F(n: nat): nat
      decreases {:global} n
  }

  // Sealed: only this module may implement G.
  trait S {
    function G(n: nat): nat
  }

  function UseS(s: S, n: nat): nat
  {
    s.G(n)
  }

  function Sum(t: T, n: nat): nat
    decreases {:global} n
  {
    if n == 0 then 0 else t.F(n - 1) + 1
  }

  function Above(t: T, n: nat): nat
    decreases {:global} n + 1
  {
    t.F(n)
  }
}
