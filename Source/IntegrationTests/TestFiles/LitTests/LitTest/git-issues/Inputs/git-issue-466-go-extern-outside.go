package type_
import "dafny"
type tools struct{}
var Tools = tools{}
func (tools) Six() dafny.Int { return dafny.IntOf(6) }
