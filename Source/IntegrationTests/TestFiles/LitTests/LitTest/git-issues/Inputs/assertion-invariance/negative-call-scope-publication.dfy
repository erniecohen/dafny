method FalseReq()
  requires false
{}

method ScopeFalseProbe() {
  FalseReq();
}
