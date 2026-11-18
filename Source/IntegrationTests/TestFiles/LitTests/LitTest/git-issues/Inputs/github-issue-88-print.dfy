// The resolved program is printed (--rprint) with the parents that a refining declaration adds, and only
// those, after `...`.  The parents taken from the refined declaration are not repeated, also not for a
// declaration that a further refinement inherits unchanged, and not for a class that adds no parent.

module A {
  trait P { function G(): int }
  trait T extends P { }
  class K extends P { function G(): int { 0 } }
}
module B refines A {
  trait S { function F(): int }
  trait T ... extends S { }
  class K ... { }
}
module C refines B {
  trait T ... { function H(): int { F() + G() } }
}
module D refines B { }

// CHECK-L: module B refines A {
// CHECK-L: trait T ... extends S { }
// CHECK-L: class K ... {
// CHECK-L: module C refines B {
// CHECK-L: trait T ... {
// CHECK-L: class K ... {
// CHECK-L: module D refines B {
// CHECK-L: trait T ... extends S { }
// CHECK-L: class K ... {
