using Xunit;

// CaptureDatasetRecorderTests overrides SCREENSTAT_DATASET_DIR for the whole
// process, and DatasetRegressionTests resolves its dataset root from that same
// variable. Running the classes in parallel would let the recorder tests
// silently redirect the regression run to an empty temporary directory, which
// would pass while testing nothing.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
