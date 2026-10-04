// Private unexecuted completeness control for assumption withholding.
// This has no explicit bad cast. It must not supply an inhabitant of Impossible.
// Exact resolver/verification rejection is unmeasured.
type Impossible = x: int | false witness *
codatatype Outer = Outer(tail: Outer, field: Impossible)
function Bad(): Outer {
  Outer(Outer(Bad(), Bad().field), Bad().field)
}
lemma Exploit() { var s := Bad(); assert false; }
