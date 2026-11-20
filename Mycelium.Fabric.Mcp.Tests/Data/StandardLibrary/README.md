# Part of the SysML v2 standard library, for the tests

`StandardLibrarySubset.json` holds, in the JSON of SysML2.NET, the elements of the SysML v2 standard library that the
tests use: `ModelingMetadata::Refinement`, `ISQBase::MassValue`, `Ports::Port`, some units of `SI` and `USCustomaryUnits`
with the units that their conversions use, and the owners of these elements and the types that they specialize (about
1,600 elements). The tests read it instead of the whole library (about 110,000 elements), which is not in the repository:
the MCP server reads the library from the folder set by `FabricMcp:LibraryDirectory`.

It was extracted with SysML2.NET 0.26.0 from the XMI files of the standard library of the OMG SysML v2 release of August
2026 (`sysml.library.xmi` of https://github.com/Systems-Modeling/SysML-v2-Release). The library is licensed under the
Eclipse Public License 2.0 (see `LICENSE`).
