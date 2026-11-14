// Reads of constants that remain allowed (https://github.com/erniecohen/dafny/issues/339).
datatype D = Zero | One(D)

// Before "new;": an initializer that does not mention "this"
class Independent {
  static const g: int := 7
  const k: int := 5
  const m: int := g + 1
  const t: int
  const u: int
  constructor ()
    ensures t == 6 && u == 8
  {
    t := k + 1;
    var x := m;
    u := x;
    new;
  }
}

// After "new;": any constant, with the final values of the fields
class AfterNew {
  const t: D
  const s: D := t
  const u: D := One(s)
  constructor ()
    ensures s == t == One(Zero) && u == One(t)
  {
    t := One(Zero);
    new;
    assert s == t;
    assert u == One(One(Zero));
  }
}

// A class in which the constant is not read before "new;"
class NotRead {
  const t: D
  const s: D := t
  constructor (d: D)
    ensures s == d
  {
    t := d;
  }
}

// A control: the constant's value is the field's final value, and no more
method Control(a: AfterNew) {
  assert a.s == a.t;
  assert a.s != a.t;  // error
}
