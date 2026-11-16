// Used by git-issues/github-issue-422.dfy: the witness's type is underspecified, which is reported the same
// way with a constraint variable, without one, and for a subset type.

newtype N = int witness (var s := {}; 0)
newtype M = m: int | true witness (var s := {}; 0)
type S = x: int | true witness (var s := {}; 0)
