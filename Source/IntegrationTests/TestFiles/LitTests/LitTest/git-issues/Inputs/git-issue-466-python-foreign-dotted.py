# Module: Foreign.Nested
class default__:
    @staticmethod
    def F():
        import B
        return B.default__.Base() + 1
