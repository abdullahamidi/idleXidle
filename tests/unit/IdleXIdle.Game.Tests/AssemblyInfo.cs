// UiMetrics and UiKit.Page are process-wide statics — exactly what the game has, one profile at a time.
// xUnit runs test CLASSES in parallel by default, so one class applying 150 % while another asserts
// 100 % read each other's profile. The tests here are milliseconds; they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
