// The integration suite shares one in-memory database per factory instance and boots a
// real host; running collections in parallel makes the InMemory provider contend on the
// same store. Serialize the assembly so results are deterministic.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
