// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Xunit;

namespace VhcXTests
{
    // Serializes every test class that mutates shared mutable static state
    // (CGlobals.desiredPath / IMPORT / IMPORT_PATH / Scrubber, CVariables.ResolvedImportPath,
    // the static CLogger, and CredentialStore's static _cache/StorePath). xUnit parallelizes
    // test classes across threads by default,
    // and the ctor/Dispose save-restore pattern only guards SEQUENTIAL leakage on one thread —
    // it cannot stop a concurrent thread mutating the same statics. Placing all such classes in
    // this single DisableParallelization collection makes the suite deterministic without
    // serializing the rest of the (parallel-safe) tests.
    [CollectionDefinition("GlobalState", DisableParallelization = true)]
    public class GlobalStateCollection { }
}
