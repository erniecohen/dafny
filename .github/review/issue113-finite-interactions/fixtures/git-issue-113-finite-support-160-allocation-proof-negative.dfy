// Private source-only draft; no change to the arbitrary allocation contract.
// Existing132+160 guards must remain effective through nominal datatype fields.
class C {}
datatype Captured = Captured(reference: C)
newtype WrappedCapture = Captured witness *

method FreshCapture()
{
  var c := new C;
  var nominal := Captured(c) as WrappedCapture;
  var f: () -> WrappedCapture := () => nominal;
  assert old(allocated(f)); // Intended soundness rejection: the fresh capture was unavailable.
}

method MappedFreshCapture() {
  var c := new C;
  var nominal := Captured(c) as WrappedCapture;
  var callbacks := map i: int | i == 0 :: i := (() => nominal);
  assert old(allocated(callbacks[0]));
}
