package map_
import "dafny"
type tools struct{}
var Tools = tools{}
func (tools) Five() dafny.Int { return dafny.IntOf(5) }
