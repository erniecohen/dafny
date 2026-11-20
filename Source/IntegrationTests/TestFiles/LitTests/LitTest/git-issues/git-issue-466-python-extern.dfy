// Whole foreign member modules preserve their ordinary Python interfaces.
// NONUNIFORM: Python group linkage admission.
// RUN: %exits-with -any %rm -rf "%t-interface/module-py"
// RUN: %exits-with -any %rm -rf "%t-interface/type-py"
// RUN: %exits-with -any %rm -rf "%t-interface/member-py"
// RUN: %exits-with -any %rm -rf "%t-interface/omitted-module-py"
// RUN: %exits-with -any %rm -rf "%t-interface/omitted-type-py"
// RUN: %exits-with -any %rm -rf "%t-interface/omitted-member-py"
// RUN: %build --no-verify --target py --member-invariants --recursive-modules "%s" --output "%t-interface/module" > "%t"
// RUN: %OutputCheck --file-to-check "%t-interface/module-py/_dafny_group_B.py" "%s"
// RUN: %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-go-extern-type.dfy" --output "%t-interface/type" >> "%t"
// RUN: %OutputCheck --file-to-check "%t-interface/type-py/_dafny_group_B.py" "%s"
// RUN: %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-go-extern-member.dfy" --output "%t-interface/member" >> "%t"
// RUN: %OutputCheck --file-to-check "%t-interface/member-py/_dafny_group_B.py" "%s"
// RUN: %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-omitted-module.dfy" --output "%t-interface/omitted-module" >> "%t"
// RUN: %OutputCheck --file-to-check "%t-interface/omitted-module-py/_dafny_group_B.py" "%s"
// RUN: %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-omitted-type.dfy" --output "%t-interface/omitted-type" >> "%t"
// RUN: %OutputCheck --file-to-check "%t-interface/omitted-type-py/_dafny_group_B.py" "%s"
// RUN: %build --no-verify --target py --member-invariants --recursive-modules "%S/Inputs/git-issue-466-python-omitted-member.dfy" --output "%t-interface/omitted-member" >> "%t"
// RUN: %OutputCheck --file-to-check "%t-interface/omitted-member-py/_dafny_group_B.py" "%s"
// RUN: %exits-with 3 %translate py %trargs --no-verify --member-invariants --recursive-modules --python-module-name ForeignPackage "%S/Inputs/git-issue-466-python-all-omitted.dfy" --output "%t-omitted-package" >> "%t"
// RUN: %diff "%s.expect" "%t"
module {:extern} A { import B function F(): int { 1 } }
module B { import A function G(): int { A.F() } }
// The foreign member has no generated declarations; its sibling still does.
// CHECK-NOT: class _module0_
// CHECK: import A as A
// CHECK: class _module1_default__
// CHECK-NOT: class _module0_
