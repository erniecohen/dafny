# Module: Omitted
class default__:
    @staticmethod
    def Value():
        from importlib import import_module
        OmittedPeer = import_module(".OmittedPeer", __package__) if __package__ else import_module("OmittedPeer")
        return OmittedPeer.default__.Base() + 1
