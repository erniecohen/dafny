// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class Cell { constructor() {} }
newtype Capture = () -> Cell witness *
newtype General = () ~> int witness (() => 0)
datatype NominalBox = NominalBox(value: Capture)

method PreallocatedCaptureControl() {
  var n := new Cell();
  label L:
  var f := () => n;
  var nominal := f as Capture;
  var box := NominalBox(nominal);
  assert old@L(allocated(n));
  assert old@L(allocated(nominal));
  assert old@L(allocated(box));
  assert old@L(allocated(nominal()));
  assert nominal() == n;
}

method PreallocatedFrameControl() {
  var n := new Cell();
  label L:
  var f := () reads n => 0;
  var nominal := f as General;
  assert old@L(allocated(nominal));
  assert old@L(n in nominal.reads());
  assert nominal() == 0;
}
