package Foreign
import "dafny"
func Plain(x dafny.Int) dafny.Int { return x.Plus(dafny.One) }
func Renamed(x dafny.Int) dafny.Int { return x.Plus(dafny.IntOf(3)) }
type Actual struct{}
func (*Actual) Value() dafny.Int { return dafny.IntOf(13) }
type factory struct{}
var Companion_Actual_ = factory{}
func (factory) Make() *Actual { return &Actual{} }
