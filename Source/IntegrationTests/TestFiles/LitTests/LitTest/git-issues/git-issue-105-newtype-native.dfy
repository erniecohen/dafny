// RUN: %exits-with 2 %resolve --show-hints --type-system-refresh=true --general-traits=full --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the native type of a newtype.
// "raw N" is ghost, so a compiled value of N satisfies its invariants, and the native type is read
// from the header constraint and the bodies of the invariants N declares, with "this" as the
// variable.  Invariants inherited from a trait give no bound, but keep the type from being
// target-complete, as an invariant that gives no bound does (Exact).  A newtype based on "raw N"
// (OverRaw) has the values of "raw N", which nothing bounds.  An explicit {:nativeType} is confirmed
// from the invariants (Explicit), and the negative control TooBig is not.

newtype Byte = x: int | 0 <= x < 256 { invariant this as int != 7  witness 1 }
newtype Member = int { invariant 0 <= this as int < 256  witness 0 }
newtype Member2 = int { invariant 0 <= this as int  invariant this < 0x1_0000 as raw Member2  witness 0 }
newtype Rich = int { invariant 0 <= this as int < 10 && IsOk()  ghost predicate IsOk() { this as int != 3 } witness 0 }
newtype Plain = int { invariant this as int % 2 == 0  witness 0 }
newtype Over = Byte
newtype OverRaw = raw Member
newtype Exact = x: int | 0 <= x < 256 { invariant true  witness 0 }
trait T { const c := 1  invariant c > 0 }
newtype Inh extends T = x: int | 0 <= x < 256
newtype BV = bv8 { invariant this != 7  witness 0 }
newtype {:nativeType "byte"} Explicit = int { invariant 0 <= this as int < 256  witness 0 }
newtype {:nativeType "byte"} TooBig = int { invariant 0 <= this as int < 257  witness 0 }
