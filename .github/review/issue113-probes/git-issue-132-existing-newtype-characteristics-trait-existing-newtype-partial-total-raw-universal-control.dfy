// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
// Keep this independent control unchanged: it isolates the existing arrow-allocation proof boundary.
type DirectPartial(!new) = int --> int
type DirectTotal(!new) = int -> int

lemma RawPartialUnassisted()
  ensures forall f: DirectPartial {:trigger allocated(f)} :: allocated(f)
{}

lemma RawTotalUnassisted()
  ensures forall f: DirectTotal {:trigger allocated(f)} :: allocated(f)
{}
