// Intrinsic declarations shared by the issue-374 domain controls.

module D0 {
  ghost function {:functionDomain "restrict"} Restrict<B>(f: () -> B): () -> B
  ghost predicate {:functionDomain "exactDom"} ExactDom<B>(f: () -> B)
  ghost predicate {:no_inline} {:functionDomain "equal"} Equal<B>(f: () -> B, g: () -> B) { f == g }
}

module D2 {
  ghost function {:functionDomain "restrict"} Restrict<A0, A1, B>(f: (A0, A1) -> B): (A0, A1) -> B
  ghost predicate {:functionDomain "exactDom"} ExactDom<A0, A1, B>(f: (A0, A1) -> B)
  ghost predicate {:no_inline} {:functionDomain "equal"} Equal<A0, A1, B>(f: (A0, A1) -> B, g: (A0, A1) -> B) { f == g }
}

module D5 {
  ghost function {:functionDomain "restrict"} Restrict<A0, A1, A2, A3, A4, B>(f: (A0, A1, A2, A3, A4) -> B): (A0, A1, A2, A3, A4) -> B
  ghost predicate {:functionDomain "exactDom"} ExactDom<A0, A1, A2, A3, A4, B>(f: (A0, A1, A2, A3, A4) -> B)
  ghost predicate {:no_inline} {:functionDomain "equal"} Equal<A0, A1, A2, A3, A4, B>(f: (A0, A1, A2, A3, A4) -> B, g: (A0, A1, A2, A3, A4) -> B) { f == g }
}
