package Foreign

import "dafny"

type tools struct{}
var Tools = tools{}
func (tools) Identity(x dafny.Int) dafny.Int { return x }
