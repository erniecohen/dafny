// RUN: %exits-with 2 %verify "%s" > "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 0 %verify --legacy-replacement-cycles "%s" >> "%t"
// RUN: %exits-with 0 %verify --legacy-replacement-cycles --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 0 %verify "%S/Inputs/github-issue-405-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-405-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-replacement-cycles "%S/Inputs/github-issue-405-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %run "%S/Inputs/github-issue-405-accepted.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/405): a module that replaces a replaceable
// module could import a client of that module.  The module graph the resolver checks was acyclic, since
// the client depends on the replaceable module and not on its replacement.  But in the compiled program
// every use of the replaceable module is a use of the replacement, so the replacement and the client
// depended on each other, and a call cycle through them got no termination check: each lemma K below
// proved false, with every lemma given a body and no assumption in the program.
// Now a replacement is an error when it uses the module it replaces other than by replacing it: by
// importing it, or by depending on a module that depends on it, directly or through other replacements.
// Each program below is one such replacement and is rejected, under both resolvers.  Under the hidden
// --legacy-replacement-cycles all of them verify, as before.  Inputs/github-issue-405-accepted.dfy
// has replacements that the rule accepts.

// The issue's program: lemmas.
replaceable module Foo { lemma L() ensures false }
module Bar { import Foo  lemma K() ensures false { Foo.L(); } }
module FooImpl replaces Foo { import Bar  lemma L() ensures false { Bar.K(); } }
module Main {
  import Bar
  method Main() {
    var a := new int[0];
    Bar.K();
    print a[0], "\n";
  }
}

// Functions: F(0) == G(0) == F(0) + 1.
replaceable module FunFoo { function G(n: nat): nat }
module FunBar { import FunFoo  function F(n: nat): nat { FunFoo.G(n) } }
module FunFooImpl replaces FunFoo { import FunBar  function G(n: nat): nat { FunBar.F(n) + 1 } }

// Types: each datatype needs a value of the other, so neither has a value.
replaceable module TypeFoo { type T(0) }
module TypeBar { import TypeFoo  datatype U = U(t: TypeFoo.T) }
module TypeFooImpl replaces TypeFoo { import TypeBar  datatype T = T(u: TypeBar.U) }

// Through two intermediate modules.
replaceable module HopFoo { lemma L() ensures false }
module HopBar { import HopFoo  lemma K() ensures false { HopFoo.L(); } }
module Hop1 { import HopBar  lemma K1() ensures false { HopBar.K(); } }
module Hop2 { import Hop1  lemma K2() ensures false { Hop1.K1(); } }
module HopFooImpl replaces HopFoo { import Hop2  lemma L() ensures false { Hop2.K2(); } }

// The replacement imports the module it replaces: the call F.L() is a call of SelfFooImpl.L itself.
replaceable module SelfFoo { lemma L() ensures false }
module SelfFooImpl replaces SelfFoo { import F = SelfFoo  lemma L() ensures false { F.L(); } }

// A module nested in the replacement imports the replaced module.
replaceable module SubFoo { lemma L() ensures false }
module SubFooImpl replaces SubFoo {
  module Sub { import SubFoo  lemma K() ensures false { SubFoo.L(); } }
  lemma L() ensures false { Sub.K(); }
}

// The replaced module is nested, the enclosing module uses it, and the replacement imports the
// enclosing module.
module NestOuter {
  replaceable module F { lemma L() ensures false }
  lemma K() ensures false { F.L(); }
}
module NestImpl replaces NestOuter.F { import NestOuter  lemma L() ensures false { NestOuter.K(); } }

// A chain of replacements: ChainTop stands for ChainMid and for ChainFoo.
replaceable module ChainFoo { lemma L() ensures false }
replaceable module ChainMid replaces ChainFoo { }
module ChainBar { import ChainFoo  lemma K() ensures false { ChainFoo.L(); } }
module ChainTop replaces ChainMid { import ChainBar  lemma L() ensures false { ChainBar.K(); } }

// Two replacements, cyclic only together: PairImpl1 uses PairUser1, which uses PairFoo2, which
// PairImpl2 replaces; PairImpl2 uses PairUser2, which uses PairFoo1, which PairImpl1 replaces.
replaceable module PairFoo1 { lemma L1() ensures false }
replaceable module PairFoo2 { lemma L2() ensures false }
module PairUser1 { import PairFoo2  lemma K1() ensures false { PairFoo2.L2(); } }
module PairUser2 { import PairFoo1  lemma K2() ensures false { PairFoo1.L1(); } }
module PairImpl1 replaces PairFoo1 { import PairUser1  lemma L1() ensures false { PairUser1.K1(); } }
module PairImpl2 replaces PairFoo2 { import PairUser2  lemma L2() ensures false { PairUser2.K2(); } }

// The client imports an export set of the replaced module, opened.
replaceable module ExportFoo { export Pub provides L  lemma L() ensures false }
module ExportBar { import opened ExportFoo`Pub  lemma K() ensures false { L(); } }
module ExportFooImpl replaces ExportFoo { import ExportBar  lemma L() ensures false { ExportBar.K(); } }

// The client's import of the replaced module is inherited from the module the client refines.
replaceable module RefineFoo { lemma L() ensures false }
abstract module RefineBase { import RefineFoo  lemma K() ensures false { RefineFoo.L(); } }
module RefineBar refines RefineBase { }
module RefineFooImpl replaces RefineFoo { import RefineBar  lemma L() ensures false { RefineBar.K(); } }
