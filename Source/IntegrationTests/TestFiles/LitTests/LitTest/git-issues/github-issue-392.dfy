// A module's generated names (bound variables, locals, lets, labels, quantifier and proof-dependency
// ids) carry a short prefix, not the hex of the module's full name, which doubled the printed Boogie
// (https://github.com/erniecohen/dafny/issues/392).  A name in a module of more than three bytes
// costs "M", six base-32 characters of the SHA-256 of the module's name, and "_", however long the
// name is; the default module has no prefix at all.
// RUN: %verify --bprint "%t.bpl" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %OutputCheck --file-to-check "%t_AModuleWhoseQualifiedNameIsLongerThanAnyPrefix_mWithANestedModuleInside.bpl" "%s"
// RUN: %OutputCheck --file-to-check "%t__module.bpl" "%S/Inputs/github-issue-392-default.check"

// CHECK-NOT: someBoundVariable#[A-Za-z0-9]{9}
// CHECK: someBoundVariable#Mmg5y2g_0
// CHECK-NOT: someBoundVariable#[A-Za-z0-9]{9}

module AModuleWhoseQualifiedNameIsLongerThanAnyPrefix {
  module WithANestedModuleInside {
    lemma L(s: seq<int>)
      requires forall someBoundVariable :: 0 <= someBoundVariable < |s| ==> s[someBoundVariable] > 0
      ensures forall i :: 0 <= i < |s| ==> s[i] >= 0
    {
    }
  }
}

lemma InTheDefaultModule(s: seq<int>)
  requires forall someBoundVariable :: 0 <= someBoundVariable < |s| ==> s[someBoundVariable] > 0
  ensures forall i :: 0 <= i < |s| ==> s[i] >= 0
{
}
