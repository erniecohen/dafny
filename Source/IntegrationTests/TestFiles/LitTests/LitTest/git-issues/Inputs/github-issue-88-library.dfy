// A library whose own source adds a parent in a refinement, so it is built with --refinement-extends.

module Lib0 {
  trait T extends object {
    function Size(): nat ensures Size() % 2 == 0
  }
  class C extends T {
    constructor () {}
    function Size(): nat ensures Size() % 2 == 0 { 2 }
  }
}

module LibWeak {
  trait T extends object {
    function Size(): nat
  }
  class C extends T {
    constructor () {}
    function Size(): nat { 2 }
  }
}

module Lib1 refines Lib0 {
  trait Sized extends object {
    function Size(): nat
  }
  trait T ... extends Sized {}
}

module LibTraits {
  trait Named extends object {
    function Name(): string
    function Length(): nat { |Name()| }
  }
}
