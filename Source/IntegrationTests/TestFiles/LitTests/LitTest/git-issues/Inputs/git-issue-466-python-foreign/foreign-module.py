# Module: ForeignModule
class default__:
    @staticmethod
    def foreign_value():
        from importlib import import_module
        Peer = import_module(".Peer", __package__) if __package__ else import_module("Peer")
        return Peer.default__.Base() + 2
    @staticmethod
    def Callback():
        from importlib import import_module
        Peer = import_module(".Peer", __package__) if __package__ else import_module("Peer")
        return Peer.default__.Base() + 1
