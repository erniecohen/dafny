// An abstract module whose concrete method body a refinement would inherit.
abstract module M1 { const {:termination_closed} CertificateRequested: bool := true
  method B()
  method A() { }
}
