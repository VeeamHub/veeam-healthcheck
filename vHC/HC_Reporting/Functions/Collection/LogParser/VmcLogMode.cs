// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    // Which product's VMC.log install ID is being looked up. Deliberately separate from
    // VeeamHealthCheck.Shared.TargetProduct - that enum's Auto/Both values describe a CLI
    // scoping choice, not a single lookup, and would be meaningless as a dictionary key here.
    internal enum VmcLogMode
    {
        Vbr,
        Vb365
    }
}
