// Regression for opt-in legacy casts to subsets of inductive datatypes.
// RUN: %exits-with 0 %baredafny verify "%S/Inputs/git-issue-961-positive.dfy" --type-system-refresh:false --general-newtypes:false --datatype-subset-conversions --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1
