import sys

sys.dont_write_bytecode = True

from App.Main import RunApplication


if __name__ == "__main__":
    RaiseCode = RunApplication()
    raise SystemExit(RaiseCode)
