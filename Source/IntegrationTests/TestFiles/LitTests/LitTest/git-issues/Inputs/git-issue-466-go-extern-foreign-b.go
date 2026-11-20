package Foreign
import "dafny"
type tools struct{}
var Tools = tools{}
func (tools) Twice(x dafny.Int) dafny.Int { return x.Times(dafny.IntOf(2)) }
func (tools) Triple(x dafny.Int) dafny.Int { return x.Times(dafny.IntOf(3)) }
func (tools) Identity(_td dafny.TypeDescriptor, x interface{}) interface{} { return x }
func (tools) Default(_td dafny.TypeDescriptor) interface{} { return _td.Default() }
