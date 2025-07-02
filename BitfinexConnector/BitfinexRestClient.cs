using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TestHQ;

namespace BitfinexConnector
{
    public class BitfinexRestClient
    {
        //Перед торговыми парами ставится буква «t» (например, tBTCUSD, tETHUSD, ...). How much was bought (positive) or sold (negative)
        //Перед валютами с кредитным плечом ставится буква «f» (например, fUSD, fBTC, ...).
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api-pub.bitfinex.com/v2";
        public BitfinexRestClient()
        {
            _httpClient = new HttpClient();
        }
        public async Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
        {
            var url = GetTradesUrlString(pair, maxCount);
            var response = await _httpClient.GetStreamAsync(url);
            Thread.Sleep(500);
            var trades = JsonSerializer.Deserialize<List<List<JsonElement>>>(response);
            if (trades == null || !trades.Any())
                throw new Exception();
            return trades!.Select(t => new Trade
            {
                Id = t[0].ToString(), 
                Time = DateTimeOffset.FromUnixTimeMilliseconds(t[1].GetInt64()), 
                Amount = t[2].GetDecimal(), 
                Price = t[3].GetDecimal(), 
                Side = t[2].GetDecimal() > 0 ? "buy" : "sell", 
                Pair = pair
            });

        }
        /// <summary>
        /// Return url string for getting data.
        /// </summary>
        /// <param name="pair">Value pair</param>
        /// <param name="maxCount">Max count zaprosov</param>
        /// <returns></returns>
        string GetTradesUrlString(string pair, int maxCount)
        {
            return $"{_baseUrl}/trades/t{pair}/hist?limit={maxCount}";
        }
        public async Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from, DateTimeOffset? to, long? count)
        {
            //Available values: "1m", "5m", "15m", "30m", "1h", "3h", "6h", "12h", "1D", "1W", "14D", "1M".
            var timeframe = periodInSec switch
            {
                60 => "1m",
                300 => "5m",
                900 => "15m",
                3600 => "1h",
                _ => throw new ArgumentException("Unsupported timeframe")
            };
            //По умолчанию конечная точка предоставляет данные за последние 100 свечей, но можно указать лимит, а также время начала и/или окончания.
            var url = $"{_baseUrl}/candles/trade:{timeframe}:t{pair}/hist?limit={count ?? 100}";
            if (from.HasValue) url += $"&start={from.Value.ToUnixTimeMilliseconds()}";
            if (to.HasValue) url += $"&end={to.Value.ToUnixTimeMilliseconds()}";

            var response = await _httpClient.GetStringAsync(url);
            var candles = JsonSerializer.Deserialize<List<List<decimal>>>(response);
            return candles!.Select(c => new Candle
            {
                Pair = pair,
                OpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)c[0]),
                OpenPrice = c[1],
                ClosePrice = c[2],
                HighPrice = c[3],
                LowPrice = c[4],
                TotalVolume = c[5],
                TotalPrice = c[2] * c[5] // Пример расчета
            });
        }
        /// <summary>
        /// Return url string for getting data.
        /// </summary>
        /// <param name="timeFrame">Available values: "1m", "5m", "15m", "30m", "1h", "3h", "6h", "12h", "1D", "1W", "14D", "1M".</param>
        /// <param name="pair">Value pair</param>
        /// <param name="count">max count zaprosov</param>
        /// <returns></returns>
        string GetCandleUrlString(string timeFrame, string pair, long? count)
        {
            return $"{_baseUrl}/candles/trade:{timeFrame}:t{pair}/hist?limit={count ?? 100}";
        }
    }
    
}
