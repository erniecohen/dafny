// A user-written {:auto_generated} does not hide {:axiom} or {:verify false} from the auditor.
// The reveal lemma generated for g, which Dafny marks {:axiom} {:verify false}, is not reported.
lemma {:axiom} {:auto_generated} Ax() ensures false
lemma {:verify false} {:auto_generated} Vf() ensures false { }
opaque function g(): int { 1 }
