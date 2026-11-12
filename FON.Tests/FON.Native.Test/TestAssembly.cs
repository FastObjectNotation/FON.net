using Xunit;

// The native maximum depth is process-wide. A test that changes it must not run while a
// test of another class is parsing, so the classes run in turn.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
