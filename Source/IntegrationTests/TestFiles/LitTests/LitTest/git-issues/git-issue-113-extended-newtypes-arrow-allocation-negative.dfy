// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

class Cell {
  var value: int
  constructor(value: int) ensures this.value == value { this.value := value; }
  method Set5() modifies this ensures value == 5 { value := 5; }
}

newtype Capture = () -> Cell witness *
newtype ReadsCapture = () ~> int witness (() => 0)
newtype Wrap<!T> = T witness *
newtype DoubleCapture = Capture witness *
datatype NominalBox = NominalBox(value: Capture)
datatype CallableBox = CallableBox(value: () ~> int)
datatype RefHolder = RefHolder(value: Cell)
newtype Holder = RefHolder witness *

function Identity<T>(value: T): T { value }

method EmptyReadsCaptureMustNotAllocateInPast() ensures false {
  label L:
  var n := new Cell(0);
  var f := () => n;
  var nominal := f as Capture;
  assert old@L(allocated(nominal)); // EXPECT_FAIL: f captured n after L
  assert old@L(allocated(nominal()));
  assert !old@L(allocated(n));
}

method NominalDatatypeMustNotIntroduceAllocation() ensures false {
  label L:
  var n := new Cell(0);
  var nominal := (() => n) as Capture;
  var box := NominalBox(nominal);
  assert old@L(allocated(box)); // EXPECT_FAIL: nominal field may involve references
  assert old@L(allocated(box.value));
  assert old@L(allocated(box.value()));
  assert !old@L(allocated(n));
}

method SignatureDoesNotDescribeCapture() ensures false {
  label L:
  var n := new Cell(0);
  var f := () reads n => 0;
  var box := CallableBox(f);
  assert old@L(allocated(box)); // EXPECT_FAIL: arrow signature has no refs but capture does
  assert old@L(allocated(box.value));
  assert old@L(n in box.value.reads());
  assert !old@L(allocated(n));
}

method GenericBoxBoundaryMustNotRecoverIntroduction() ensures false {
  label L:
  var n := new Cell(0);
  var generic := (() => n) as Wrap<() -> Cell>;
  var transferred := Identity(generic);
  assert old@L(allocated(transferred)); // EXPECT_FAIL: exercise generic box/unbox boundary
  assert old@L(allocated((transferred as () -> Cell)()));
  assert !old@L(allocated(n));
}

method DoubleNominalBoundaryMustNotRecoverIntroduction() ensures false {
  label L:
  var n := new Cell(0);
  var once := (() => n) as Capture;
  var twice := once as DoubleCapture;
  assert old@L(allocated(twice)); // EXPECT_FAIL: each wrapper forwards the same allocation
  assert old@L(allocated(twice()));
  assert !old@L(allocated(n));
}

method NestedContainerMustNotRecoverIntroduction() ensures false {
  label L:
  var n := new Cell(0);
  var nominal := (() => n) as Capture;
  ghost var nested: seq<set<Capture>> := [{nominal}];
  assert old@L(allocated(nested)); // EXPECT_FAIL: nested boxed member captures n
  assert old@L(allocated(nominal));
  assert old@L(allocated(nominal()));
  assert !old@L(allocated(n));
}

method DatatypeNewtypeMustNotBecomeReferenceFree() ensures false {
  label L:
  var n := new Cell(0);
  var nominal := RefHolder(n) as Holder;
  assert old@L(allocated(nominal)); // EXPECT_FAIL: base datatype contains n
  assert old@L(allocated(nominal.value));
  assert !old@L(allocated(n));
}

method HeapDependentReadsNested(c: Cell)
  requires c.value != 5
  modifies c
  ensures false
{
  label L:
  c.Set5();
  label K:
  var n := new Cell(0);
  var f := () reads c, (if c.value == 5 then {n} else {}) => 0;
  var nominal := f as ReadsCapture;
  ghost var nested: seq<set<ReadsCapture>> := [{nominal}];
  assert old@L(allocated(nested)); // EXPECT_FAIL: hidden capture is after L
  assert old@K(allocated(nominal));
  assert old@K(n in nominal.reads());
  assert !old@K(allocated(n));
}

// Explicit satisfiability control: construct a concrete state and omit all
// contradiction assertions. This false postcondition must fail independently.
method SatisfiableNoAssertionsControl() ensures false {
  var c := new Cell(0);
  label L:
  c.Set5();
  label K:
  var n := new Cell(0);
  var f := () reads c, (if c.value == 5 then {n} else {}) => 0;
  var nominal := f as ReadsCapture;
}

// Constant-frame control: the frame itself requires the newly allocated object;
// even the old reads-based theory should not derive past allocation here.
method ConstantFrameControl() ensures false {
  label L:
  var n := new Cell(0);
  var nominal := (() reads n => 0) as ReadsCapture;
  assert old@L(allocated(nominal)); // EXPECT_FAIL: n is unallocated at L
  assert false;
}
