using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TestHQ;

namespace BitfinexConnector
{
    //https://docs.bitfinex.com/reference/ws-public-trades
    //https://docs.bitfinex.com/reference/ws-public-candles
    /*
        // request
        {
           event: "subscribe",
           channel: "candles",
           key: "trade:1m:tBTCUSD"
        }

        // response
        {
          event: "subscribed",
          channel: "candles",
          chanId": CHANNEL_ID,
          key: "trade:1m:tBTCUSD"
        }
     */
    public class BitfinexWebSocketClient
    {
        private readonly ClientWebSocket _webSocket;
        private readonly string _wsUrl = "wss://api-pub.bitfinex.com/ws/2";
        private readonly Dictionary<string, int> _channelIds = new();

        public event Action<Trade>? NewBuyTrade;

        public event Action<Trade>? NewSellTrade;

        public event Action<Candle>? CandleSeriesProcessing;

        public BitfinexWebSocketClient()
        {
            _webSocket = new ClientWebSocket();
        }

        public async Task ConnectAsync()
        {
            await _webSocket.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);
            _ = ReceiveMessagesAsync();
        }

        /// <summary>
        /// Subscribes to trade updates for a specific trading pair.
        /// </summary>
        /// <param name="pair">The trading pair (e.g., "BTCUSD").</param>
        /// <param name="maxCount">The maximum number of trades to subscribe. Default is 100.</param>
        public async Task SubscribeTrades(string pair, int maxCount = 100)
        {
            var subscribeMessage = JsonSerializer.Serialize(new
            {
                @event = "subscribe",
                channel = "trades",
                symbol = $"t{pair}"
            });
            await SendMessageAsync(subscribeMessage);
        }

        /// <summary>
        /// Unsubscribes from trade updates for a specific trading pair.
        /// </summary>
        /// <param name="pair">The trading pair (e.g., "BTCUSD").</param>
        public async Task UnsubscribeTrades(string pair)
        {
            var chanId = _channelIds.GetValueOrDefault($"trades:{pair}");
            var unsubscribeMessage = JsonSerializer.Serialize(new { @event = "unsubscribe", chanId });
            await SendMessageAsync(unsubscribeMessage);
        }

        /// <summary>
        /// Subscribes to candle updates for a specific trading pair and timeframe.
        /// </summary>
        /// <param name="pair">The trading pair (e.g., "BTCUSD").</param>
        /// <param name="periodInSec">The period in seconds for the candle timeframe.</param>
        /// <param name="from">The start time for the data. Optional.</param>
        /// <param name="to">The end time for the data. Optional.</param>
        /// <param name="count">The number of candles to subscribe. Default is 0 (all available).</param>
        public async Task SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
        {
            var timeframe = periodInSec switch
            {
                60 => "1m",
                300 => "5m",
                900 => "15m",
                3600 => "1h",
                _ => throw new ArgumentException("Unsupported timeframe")
            };
            var subscribeMessage = JsonSerializer.Serialize(new
            {
                @event = "subscribe",
                channel = "candles",
                key = $"trade:{timeframe}:t{pair}"
            });
            await SendMessageAsync(subscribeMessage);
        }

        /// <summary>
        /// Unsubscribes from candle updates for a specific trading pair.
        /// </summary>
        /// <param name="pair">The trading pair (e.g., "BTCUSD").</param>
        public async Task UnsubscribeCandles(string pair)
        {
            var chanId = _channelIds.GetValueOrDefault($"candles:{pair}");
            var unsubscribeMessage = JsonSerializer.Serialize(new { @event = "unsubscribe", chanId });
            await SendMessageAsync(unsubscribeMessage);
        }

        /// <summary>
        /// Sends a message asynchronously over the WebSocket connection.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private async Task SendMessageAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <summary>
        /// Receives messages asynchronously from the WebSocket connection.
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[1024 * 4];
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var json = JsonDocument.Parse(message);

                if (json.RootElement.TryGetProperty("event", out var evt) && evt.GetString() == "subscribed")
                {
                    HandleSubscription(json);
                }
                else if (json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement[1].ValueKind == JsonValueKind.Array)
                {
                    HandleDataUpdate(json);
                }
            }
        }

        /// <summary>
        /// Handles subscription messages received from the WebSocket.
        /// </summary>
        /// <param name="json">The JSON document containing the subscription message.</param>
        private void HandleSubscription(JsonDocument json)
        {
            var chanId = json.RootElement.GetProperty("chanId").GetInt32();
            var channel = json.RootElement.GetProperty("channel").GetString();
            var pair = json.RootElement.GetProperty(channel == "candles" ? "key" : "symbol").GetString();
            _channelIds[$"{channel}:{pair}"] = chanId;
        }

        /// <summary>
        /// Handles data update messages received from the WebSocket.
        /// </summary>
        /// <param name="json">The JSON document containing the data update message.</param>
        private void HandleDataUpdate(JsonDocument json)
        {
            var channelId = json.RootElement[0].GetInt32();
            var data = json.RootElement[1];

            if (data[0].GetString() == "te" && _channelIds.ContainsValue(channelId))
            {
                HandleTradeUpdate(data, channelId);
            }
            else if (data[0].GetString() != "hb" && _channelIds.ContainsValue(channelId))
            {
                HandleCandleUpdate(data, channelId);
            }
        }

        /// <summary>
        /// Handles trade update messages received from the WebSocket.
        /// </summary>
        /// <param name="data">The JSON element containing the trade data.</param>
        /// <param name="channelId">The channel ID associated with the trade.</param>
        private void HandleTradeUpdate(JsonElement data, int channelId)
        {
            var trade = new Trade
            {
                Id = data[1][0].ToString(),
                Time = DateTimeOffset.FromUnixTimeMilliseconds(data[1][1].GetInt64()),
                Amount = data[1][2].GetDecimal(),
                Price = data[1][3].GetDecimal(),
                Side = data[1][2].GetDecimal() > 0 ? "buy" : "sell",
                Pair = _channelIds.First(x => x.Value == channelId).Key.Split(':')[1].Replace("t", "")
            };
            if (trade.Side == "buy") NewBuyTrade?.Invoke(trade);
            else NewSellTrade?.Invoke(trade);
        }

        /// <summary>
        /// Handles candle update messages received from the WebSocket.
        /// </summary>
        /// <param name="data">The JSON element containing the candle data.</param>
        /// <param name="channelId">The channel ID associated with the candle.</param>
        private void HandleCandleUpdate(JsonElement data, int channelId)
        {
            var candle = new Candle
            {
                Pair = _channelIds.First(x => x.Value == channelId).Key.Split(':')[1].Replace("t", ""),
                OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(data[1][0].GetInt64()),
                OpenPrice = data[1][1].GetDecimal(),
                ClosePrice = data[1][2].GetDecimal(),
                HighPrice = data[1][3].GetDecimal(),
                LowPrice = data[1][4].GetDecimal(),
                TotalVolume = data[1][5].GetInt64(),
                TotalPrice = data[1][2].GetDecimal() * data[1][5].GetInt64()
            };
            CandleSeriesProcessing?.Invoke(candle);
        }
    }
}