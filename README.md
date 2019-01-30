dll2lib
=======

## Summary

Produces an import library (.lib) from a target dynamic link library (.dll).

Requires `dumpbin.exe` and `lib.exe` in `%PATH%`. Easiest to run from the Visual Studio tools command prompt.
May also find dumpbin.exe in 'Program Files (x86)\\Microsoft Visual Studio' if it is not present in %PATH%

### Usage

For ease of use, a pre-built binary is provided in `dll2lib\bin\Release`, however feel free to build your own.

    dll2lib.exe <options> <dll>

    Options:

        /noclean        don't delete intermediate files
        /x64            use x64 version of dumpbin.exe
        /verbose        produce some additional output, supposed to be usefull for issue resolving

The import library file is output to the same directory as the target dll.

## Building

Open in Visual Studio 2012+ and hit build, or build from Visual Studio tools command prompt:

    msbuild /p:Configuration=Release
