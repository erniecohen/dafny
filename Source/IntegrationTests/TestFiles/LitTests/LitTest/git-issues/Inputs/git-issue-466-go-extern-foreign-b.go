package Foreign
import "dafny"
type tools struct{}
var Tools = tools{}
func (tools) Twice(x dafny.Int) dafny.Int { return x.Times(dafny.IntOf(2)) }
func (tools) Triple(x dafny.Int) dafny.Int { return x.Times(dafny.IntOf(3)) }
