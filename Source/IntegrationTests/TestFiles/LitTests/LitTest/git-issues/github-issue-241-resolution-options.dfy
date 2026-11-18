// RUN: %exits-with 2 %baredafny resolve --resolution-threads 1 --type-system-refresh:false "%S/Inputs/github-issue-241-malformed.dfy" > "%t.1"
// RUN: %diff "%s.expect" "%t.1"
// RUN: %exits-with 2 %baredafny resolve --resolution-threads 2 --type-system-refresh:false "%S/Inputs/github-issue-241-malformed.dfy" > "%t.2"
// RUN: %diff "%s.expect" "%t.2"
// RUN: %exits-with 2 %baredafny verify --resolution-threads 4 --type-system-refresh:false "%S/Inputs/github-issue-241-malformed.dfy" > "%t.4"
// RUN: %diff "%s.expect" "%t.4"
// RUN: %exits-with 2 %baredafny build --no-verify --resolution-threads 8 --type-system-refresh:false "%S/Inputs/github-issue-241-malformed.dfy" > "%t.8"
// RUN: %diff "%s.expect" "%t.8"
// RUN: %exits-with 2 %baredafny resolve --resolution-threads 1 "%S/Inputs/github-issue-241-malformed.dfy" > "%t.default"
// RUN: %diff "%s.expect" "%t.default"
// RUN: %exits-with 2 %baredafny resolve "%S/Inputs/github-issue-241-legacy/dfyconfig.toml" > "%t.project"
// RUN: %diff "%s.expect" "%t.project"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false "%S/Inputs/github-issue-241-refreshed/dfyconfig.toml" > "%t.override"
// RUN: %diff "%s.expect" "%t.override"
// RUN: %baredafny resolve --type-system-refresh "%S/Inputs/github-issue-241-legacy/dfyconfig.toml" > "%t.accepted"
// RUN: %diff "%s.accepted.expect" "%t.accepted"
// RUN: %baredafny resolve --type-system-refresh:false "%s" > "%t.off"
// RUN: %diff "%s.accepted.expect" "%t.off"

// https://github.com/erniecohen/dafny/issues/241
// Reject incompatible options before parsing, even on malformed input. Project
// values and explicit CLI overrides participate in the same validation.
