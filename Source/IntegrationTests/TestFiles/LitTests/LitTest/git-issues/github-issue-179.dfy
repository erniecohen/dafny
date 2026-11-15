// RUN: %baredafny resolve --allow-deprecation --type-system-refresh:true --general-newtypes:true --general-traits:full "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny resolve --allow-deprecation --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny verify --allow-deprecation --solver-path "%z3" --type-system-refresh:true --general-newtypes:true --general-traits:full "%s" > "%t.verify"
// RUN: %diff "%s.verify.expect" "%t.verify"

// A refining module clones each inherited method body, with the triggers that the base module
// generated for its :| statements, and resolves it again.  A trigger term that selects a member
// of a receiver kept the resolved form, which the type-system-refresh resolver cannot resolve.
// The refining module below only strengthens a method's postcondition, as in the issue.

module ExtraProperty {
  ghost predicate Holds<T>(s: seq<T>) { true }
}

module Base {
  datatype Stream<T> = Cons(head: T, tail: Stream<T>) | Nil

  ghost function Tail<T>(s: Stream<T>, n: nat): Stream<T> {
    if n == 0 || s.Nil? then s else Tail(s.tail, n - 1)
  }

  class Cell {
    var data: int
    var keys: set<int>
  }

  function Id(m: map<int, int>): map<int, int> { m }

  // A receiver that is a formal, inside a function call: Tail(s.tail, n).
  method Run<T>(s: Stream<T>, x: T) returns (before: seq<T>, after: seq<T>)
    requires s.Cons? && s.tail.Cons? && s.tail.head == x
  {
    before := [x];
    ghost var n: nat :| Tail(s.tail, n).Cons? && Tail(s.tail, n).head == x;
    after := before;
  }

  // A receiver that is a local, selected at the top of the trigger term: c.data.
  method Field(c: Cell) returns (y: int)
    requires c.data == 7
  {
    var d: Cell :| d == c && d.data == 7;
    y := d.data;
  }

  // A member of a map, under a call that the substitution does not change: Id(m).Keys.
  method Keys(m: map<int, int>) returns (k: int)
    requires 0 in m
  {
    k :| k in Id(m).Keys;
  }
}

module Refined refines Base {
  import Property = ExtraProperty

  method Run...
    ensures Property.Holds(before) ==> Property.Holds(after)
}

// The implicit receiver this: keys.
module ClassBase {
  class C {
    var keys: set<int>
    method Pick() returns (k: int)
      requires keys != {}
    {
      k :| k in keys;
    }
  }
}

module ClassRefined refines ClassBase { }
