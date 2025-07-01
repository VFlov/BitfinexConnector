using ConnectorTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestHQ;

namespace BitfinexConnector
{
    public class BitfinexConnector : ITestConnector
    {
        private readonly BitfinexRestClient _restClient;
        private readonly BitfinexWebSocketClient _wsClient;

        public event Action<Trade> NewBuyTrade
        {
            add => _wsClient.NewBuyTrade += value;
            remove => _wsClient.NewBuyTrade -= value;
        }
        public event Action<Trade> NewSellTrade
        {
            add => _wsClient.NewSellTrade += value;
            remove => _wsClient.NewSellTrade -= value;
        }
        public event Action<Candle> CandleSeriesProcessing
        {
            add => _wsClient.CandleSeriesProcessing += value;
            remove => _wsClient.CandleSeriesProcessing -= value;
        }

        public BitfinexConnector()
        {
            _restClient = new BitfinexRestClient();
            _wsClient = new BitfinexWebSocketClient();
            _wsClient.ConnectAsync().GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
        {
            try
            {
                return await _restClient.GetNewTradesAsync(pair, maxCount);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get trades: {ex.Message}");
            }
        }

        public async Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from, DateTimeOffset? to = null, long? count = 0)
        {
            try
            {
                return await _restClient.GetCandleSeriesAsync(pair, periodInSec, from, to, count);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get candles: {ex.Message}");
            }
        }

        public void SubscribeTrades(string pair, int maxCount = 100)
        {
            _wsClient.SubscribeTrades(pair, maxCount).GetAwaiter().GetResult();
        }

        public void UnsubscribeTrades(string pair)
        {
            _wsClient.UnsubscribeTrades(pair).GetAwaiter().GetResult();
        }

        public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
        {
            _wsClient.SubscribeCandles(pair, periodInSec, from, to, count).GetAwaiter().GetResult();
        }

        public void UnsubscribeCandles(string pair)
        {
            _wsClient.UnsubscribeCandles(pair).GetAwaiter().GetResult();
        }
    }
}
