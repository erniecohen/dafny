// https://github.com/erniecohen/dafny/issues/284
// RUN: %verify --unicode-char:true --solver-path %review-z3 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
lemma surrogateExclusion(c: char)
  ensures (c as int) < 0xD800 || 0xE000 <= (c as int)
{}
