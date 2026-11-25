// The copying imports and the uses of copies that the resolver rejects.

module Plain {
  export
    reveals Colour
    provides One
  export Named
    provides One, Two
  datatype Colour = Red | Green
  function One(): int { 1 }
  function Two(): int { 2 }
  function Hidden(): int { 3 }
}

// ---------- What cannot be copied

replaceable module Replaceable {
  function F(): int
}

module CopiesReplaceable {
  import X := Replaceable  // error: a replaceable module cannot be copied
}

module {:extern "ExternModule"} ExternModule {
  function F(): int { 1 }
}

module ExternFunction {
  function {:extern} F(): int
}

module ExternClass {
  class {:extern} C { }
}

module ExternMethod {
  class C {
    method {:extern} P()
  }
}

module CopiesExtern {
  import X1 := ExternModule  // error: extern
  import X2 := ExternFunction  // error: extern
  import X3 := ExternClass  // error: extern
  import X4 := ExternMethod  // error: extern
}

abstract module Abstract {
  function F(): int
}

module CopiesAbstract {
  import X := Abstract  // error: the copy is abstract, and this module is not
}

abstract module AbstractImporter {
  import X := Abstract
}

module InheritsAbstractCopy refines AbstractImporter {  // error: the inherited copy is abstract
}

// ---------- Export sets

module OnlyNamed {
  export Named
    provides F
  function F(): int { 1 }
}

module NoDefaultExport {
  import X := OnlyNamed  // error: no default export set
}

module UnknownExport {
  import X := Plain`Nope  // error: no such export set
}

module NamedLikeExportSet {
  import Named := Plain  // error: the default export set of the copy would be a second set called Named
}

module NotExported {
  import X := Plain
  import Z := Plain`Named
  function F(): int { X.One() + Z.Two() }
  function G(): int { X.Two() }  // error: the default export set does not have Two
  function H(): int { Z.Hidden() }  // error: no export set has Hidden
}

// ---------- The types of two copies are different types

module TwoCopies {
  import Plain
  import X := Plain
  import Z := Plain

  method P(x: X.Colour) {
    var z: Z.Colour := x;  // error: X.Colour is not Z.Colour
    var p: Plain.Colour := x;  // error: X.Colour is not Plain.Colour
  }
}

module OneCopy {
  import X := Plain
}

module RefinesOneCopy refines OneCopy {
  import OneCopy

  method P(x: X.Colour) {
    var y: OneCopy.X.Colour := x;  // error: this module's copy is not the copy of OneCopy
  }
}

module Shapes {
  datatype Shape = Dot | Line
}

abstract module HasAbstractImport {
  import A : Shapes
}

abstract module CopiesAbstractImport {
  import HasAbstractImport
  import X := HasAbstractImport
  import Z := HasAbstractImport

  method P(x: X.A.Shape) {
    var z: Z.A.Shape := x;  // error: the abstract import stands for an unknown module in each copy
    var h: HasAbstractImport.A.Shape := x;  // error: and for another one in the original
  }
}

// ---------- Names

module Original {
  import X := Plain
}

module RedeclaresAsCopy refines Original {
  import X := Shapes  // error: Shapes does not refine Plain
}

module RedeclaresAsImport refines Original {
  import X = Plain  // error: X is inherited
}

module RedeclaresAsModule refines Original {
  module X { }  // error: X is inherited
}

module CopyAndType {
  import X := Plain
  type X = int  // error: duplicate name
}
