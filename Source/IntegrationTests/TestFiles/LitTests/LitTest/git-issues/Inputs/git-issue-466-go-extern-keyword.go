package map_
import "dafny"
type tools struct{}
var Tools = tools{}
func (tools) Five() dafny.Int { return dafny.IntOf(5) }
type KeywordActual struct{}
func (*KeywordActual) Value() dafny.Int { return dafny.IntOf(29) }
type keywordFactory struct{}
var Companion_KeywordActual_ = keywordFactory{}
func (keywordFactory) Make() *KeywordActual { return &KeywordActual{} }
