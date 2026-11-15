// A project's --boogie arguments need to match a library's only where they can change which facts an
// imported, already verified declaration contributes (https://github.com/erniecohen/dafny/issues/375).
// The library-neutral arguments may differ, so Std.Axioms can be used under them.  Any other argument,
// and any unknown one, still has to match the library's.  --legacy-boogie-library-check restores
// the exact comparison.

// RUN: %verify --standard-libraries:true --boogie "/normalizeDeclarationOrder:0 /structuralLiterals" "%s" > "%t"
// RUN: %verify --standard-libraries:true --boogie /cacheBackground --boogie /scheduleByImplementation "%s" >> "%t"
// RUN: %exits-with 1 %verify --standard-libraries:true --boogie "/normalizeDeclarationOrder:0 /loopUnroll:5" "%s" >> "%t"
// RUN: %exits-with 1 %verify --standard-libraries:true --boogie "/someUnknownArgument" "%s" >> "%t"
// RUN: %exits-with 1 %verify --standard-libraries:true --boogie "/structuralLiterals:2" "%s" >> "%t"
// RUN: %exits-with 1 %verify --standard-libraries:true --legacy-boogie-library-check --boogie "/normalizeDeclarationOrder:0" "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

import Std.Axioms.FunctionExtensionality

lemma Same(f: int -> int, g: int -> int)
  requires forall x :: f(x) == g(x)
  ensures f == g
{
  FunctionExtensionality.OnInt(f, g);
}
