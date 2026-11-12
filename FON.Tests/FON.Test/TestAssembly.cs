using Xunit;

// Fon.MaxDepth and Fon.DeserializeRawUnpack are process-wide. A test that changes one of
// them must not run while a test of another class is parsing, so the classes run in turn.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
