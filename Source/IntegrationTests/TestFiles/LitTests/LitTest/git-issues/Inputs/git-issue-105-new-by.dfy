// Member invariants (https://github.com/erniecohen/dafny/issues/105): 'new by', which would prove
// facts about an unfinished object, is not yet part of the language (plan, open question 2)
class C {
  const x: int
  invariant x > 0
  constructor () ensures false {
    x := 0;
    new by {                                           // error: not yet supported
      assert false;
    }
  }
}
