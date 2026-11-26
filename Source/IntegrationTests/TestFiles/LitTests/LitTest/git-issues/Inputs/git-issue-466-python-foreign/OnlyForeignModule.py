class default__:
    @staticmethod
    def Value():
        from importlib import import_module
        AOnlyPeer = import_module(".AOnlyPeer", __package__) if __package__ else import_module("AOnlyPeer")
        return AOnlyPeer.default__.Base() + 1
