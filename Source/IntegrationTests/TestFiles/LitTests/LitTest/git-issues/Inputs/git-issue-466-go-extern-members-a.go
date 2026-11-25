package A
import "dafny"
func (*CompanionStruct_G0_Default___) Plain(x dafny.Int) dafny.Int { return x.Plus(dafny.One) }
func (*CompanionStruct_G0_Default___) Echo(x *G1_Box) *G1_Box { return x }
func (*Actual) Value() dafny.Int { return dafny.IntOf(13) }
