// RUN: %exits-with 2 %verify --solver-path %review-z3 "%s" > "%t"
// RUN: %exits-with 2 %verify --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %verify --solver-path %review-z3 "%S/Inputs/git-issue-451-a.dfy" "%S/Inputs/git-issue-451-b.dfy" >> "%t"
// RUN: %exits-with 2 %verify --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-451-a.dfy" "%S/Inputs/git-issue-451-b.dfy" >> "%t"
// RUN: %verify --solver-path %review-z3 "%S/Inputs/git-issue-451-accepted.dfy" >> "%t"
// RUN: %verify --solver-path %review-z3 --type-system-refresh=false --general-newtypes=false "%S/Inputs/git-issue-451-accepted.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/451
// The duplicate-replacement error belongs at the later source location, regardless
// of module names or dependency order. The two input files exercise file ordering.
module Independent {
  replaceable module Target {}
  module ZebraEarlier replaces Target {}
  module AlphaLater replaces Target {}
}

// The import forces the later replacement to be visited first. This must not
// reverse the duplicate-replacement diagnostic's locations.
module Dependent {
  replaceable module Target {}
  module Earlier replaces Target {}
  module Later replaces Target { import Earlier }
}
