module {:termination_closed} InvalidModule {
  method M({:termination_closed} n: nat) {}
  function {:termination_closed true} F(): nat { 0 }
  class C { var {:termination_closed} x: int }
}
