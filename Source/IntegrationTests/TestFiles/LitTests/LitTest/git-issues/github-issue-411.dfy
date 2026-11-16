// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-411-include.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-411-client.dfy" --library "%S/Inputs/github-issue-411-library.dfy" >> "%t"
// RUN: %verify "%S/Inputs/github-issue-411-positive.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/411): a refining module that adds a field
// subject to definite-assignment rules to a class keeps the constructor it inherits, which cannot
// assign the field.  The copied constructor's body asserts that the field is assigned, but dafny verify
// did not verify members a refining module inherits unchanged, so Bad proved false.  The copied
// constructor is now verified when its module adds such a field, with the module as a related
// location; every other assert of the copied body stays assumed.  This holds when the base module is
// in an included file or a library too.  Fields that need no definite assignment, and constructors the
// refining module declares itself, are unaffected (Inputs/github-issue-411-positive.dfy).

// The reproducer
module A {
  class C {
    constructor () { }
  }
}
module B refines A {
  type Empty = x: int | false witness *
  class C ... {
    var e: Empty
  }
  method Bad() ensures false {
    var c := new C();
    var e: Empty := c.e;
  }
}

// A const field
module A2 {
  class C {
    constructor () { }
  }
}
module B2 refines A2 {
  type Empty = x: int | false witness *
  class C ... {
    const e: Empty
  }
}

// A field of a class type, and a constructor with a divided body
module A3 {
  class D {
    var v: int
    constructor () { v := 0; }
  }
  class C {
    var x: int
    constructor (x0: int) ensures x == x0 { x := x0; new; assert x == x0; }
  }
}
module B3 refines A3 {
  class C ... {
    var d: D
  }
  method Main() {
    var c := new C(5);
    print c.d.v, "\n";
  }
}

// Two refining modules of one base: only the one that adds a field reports
module A4 {
  class C {
    constructor () { }
  }
}
module B4 refines A4 {
  class C ... {
    method M() { }
  }
}
module C4 refines A4 {
  class C ... {
    var w: object
  }
}

// A field added to a trait that the class extends
module A5 {
  trait T extends object { }
  class C extends T {
    constructor () { }
  }
}
module B5 refines A5 {
  type Empty = x: int | false witness *
  trait T ... {
    var t: Empty
  }
}
