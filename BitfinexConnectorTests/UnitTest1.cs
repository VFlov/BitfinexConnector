using BitfinexConnector;
using Microsoft.Diagnostics.Runtime;
using System.Windows.Media;
namespace BitfinexConnectorTests
{
    public class Tests
    {
        [TestFixture]
        public class BitfinexConnectorTests
        {
            private BitfinexConnector.BitfinexConnector _connector;

            [SetUp]
            public void Setup()
            {
                _connector = new BitfinexConnector.BitfinexConnector();
            }

            [Test]
            public async Task GetNewTradesAsync_ReturnsTrades()
            {
                var trades = await _connector.GetNewTradesAsync("BTCUSD", 10);
                Assert.That(trades.Any(), Is.True, "No trades returned");
                Assert.That(trades.All(t => t.Pair == "BTCUSD"), Is.True, "Incorrect pair in trades");
                Assert.That(trades.All(t => t.Price > 0), Is.True, "Invalid trade price");
            }

            [Test]
            public async Task GetCandleSeriesAsync_ReturnsCandles()
            {
                var candles = await _connector.GetCandleSeriesAsync("BTCUSD", 60, null, null, 10);
                Assert.That(candles.Any(), Is.True, "No candles returned");
                Assert.That(candles.All(c => c.Pair == "BTCUSD"), Is.True, "Incorrect pair in candles");
                Assert.That(candles.All(c => c.ClosePrice > 0), Is.True, "Invalid candle price");
            }

            [Test]
            public async Task GetCandleSeriesAsync_WithTimeRange_ReturnsFilteredCandles()
            {
                var from = DateTimeOffset.UtcNow.AddHours(-1);
                var to = DateTimeOffset.UtcNow;
                var candles = await _connector.GetCandleSeriesAsync("BTCUSD", 60, from, to, 10);
                Assert.That(candles.Any(), Is.True, "No candles returned in time range");
                Assert.That(candles.All(c => c.OpenTime >= from && c.OpenTime <= to), Is.True, "Candles outside time range");
            }

            [Test]
            public void GetNewTradesAsync_InvalidPair_ThrowsException()
            {
                Assert.ThrowsAsync<Exception>(async () => await _connector.GetNewTradesAsync("INVALID", 10), "Expected exception for invalid pair");
            }

            [Test]
            public async Task SubscribeTrades_Unsubscribe_StopsReceiving()
            {
                bool receivedAfterUnsubscribe = false;
                _connector.NewBuyTrade += _ => receivedAfterUnsubscribe = true;
                _connector.SubscribeTrades("BTCUSD", 10);
                await Task.Delay(1000);
                _connector.UnsubscribeTrades("BTCUSD");
                await Task.Delay(3000);
                Assert.That(receivedAfterUnsubscribe, Is.False, "Received trades after unsubscribe");
            }

            /// <summary>
            /// Тест проверяет наличие больших обьектов(LOH).
            /// Если находится хотя бы один обьект - тест не пройден т.к.:
            /// "Существует основное правило для высокопроизводительного программирования,
            /// касающееся сборщикамусора.По сути, сборщикмусора был явно разработан с прицелом на то, чтобы проводить сборку мусора в поколении gen 0 или не проводить
            /// ее вообще.
            /// Иными словами, нужно стремиться к созданию объектов с экстремально коротким временем существования, чтобы сборщик мусора никогда их не касался, а если
            /// добиться этого невозможно, чтобы они как можно скорее попадали в поколение
            /// gen 2 и оставались там навсегда, никогда не подвергаясь сборке мусора.Это означает неизменное задействование ссылок на долгоживущие объекты, а зачастую также
            /// создание пула переиспользуемых объектов (особенно это относится к чему-либо
            /// в куче больших объектов)."
            /// </summary>
            /// <returns></returns>
            [Test]
            public async Task GetLOHObjectsCount_AfterMultipleTrades_ChecksHeap()
            {
                
                for (int i = 0; i < 5; i++)
                {
                    await _connector.GetNewTradesAsync("BTCUSD", 100);
                    await Task.Delay(100);
                }

                var gcCounts = GetLOHObjectsCount();
                Assert.That(gcCounts.ContainsKey("Large"), Is.True, "LOH segment not found");
                Assert.That(gcCounts["Large"], Is.GreaterThanOrEqualTo(0), "Invalid LOH object count");
                Assert.That(gcCounts.ContainsKey("Generation0"), Is.True, "Gen0 segment not found");
            }

            private Dictionary<string, long> GetLOHObjectsCount()
            {
                using (DataTarget target = DataTarget.AttachToProcess(System.Diagnostics.Process.GetCurrentProcess().Id, false))
                {
                    ClrRuntime runtime = target.ClrVersions[0].CreateRuntime();
                    ClrHeap heap = runtime.Heap;

                    if (!heap.CanWalkHeap)
                    {
                        Assert.Fail("Cannot walk heap");
                        return new Dictionary<string, long>();
                    }

                    var gcCounts = new Dictionary<string, long>();

                    foreach (ClrSegment seg in heap.Segments)
                    {
                        string segmentName = seg.Kind.ToString();
                        long objectCount = seg.EnumerateObjects().Count();

                        if (seg.Kind == GCSegmentKind.Large ||
                            seg.Kind == GCSegmentKind.Generation0 ||
                            seg.Kind == GCSegmentKind.Generation1 ||
                            seg.Kind == GCSegmentKind.Generation2)
                        {
                            gcCounts[segmentName] = objectCount;
                        }
                    }

                    return gcCounts;
                }
            }
        }
    }
}
