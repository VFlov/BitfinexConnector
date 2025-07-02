using BitfinexConnector;
using BitfinexWPF.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitfinexWPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BitfinexRestClient _restClient;
        private ObservableCollection<PortfolioBalance> _balances;

        public ObservableCollection<PortfolioBalance> Balances
        {
            get => _balances;
            set { _balances = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _restClient = new BitfinexRestClient();
            LoadBalancesAsync();
        }

        private async void LoadBalancesAsync()
        {
            var portfolio = new Dictionary<string, decimal>
            {
                { "BTC", 1m },
                { "XRP", 15000m },
                { "XMR", 50m }
            };
            var usdBalances = await ConvertPortfolioToUSD(portfolio);
            var balances = await ConvertToTargetCurrencies(usdBalances, new[] { "USD", "BTC", "XMR" });
            Balances = balances;
        }

        private async Task<Dictionary<string, decimal>> ConvertPortfolioToUSD(Dictionary<string, decimal> portfolio)
        {
            var usdBalances = new Dictionary<string, decimal>();
            foreach (var (asset, amount) in portfolio)
            {
                if (asset == "USD")
                {
                    usdBalances[asset] = amount;
                    continue;
                }
                var price = await GetPriceAsync(asset, "USD");
                if (price.HasValue)
                    usdBalances[asset] = amount * price.Value;
            }
            return usdBalances;
        }

        private async Task<ObservableCollection<PortfolioBalance>> ConvertToTargetCurrencies(Dictionary<string, decimal> usdBalances, string[] targetCurrencies)
        {
            var balances = new ObservableCollection<PortfolioBalance>();
            foreach (var currency in targetCurrencies)
            {
                var balance = await ConvertToCurrency(usdBalances, currency);
                if (balance != null)
                    balances.Add(balance);
            }
            return balances;
        }

        private async Task<PortfolioBalance> ConvertToCurrency(Dictionary<string, decimal> usdBalances, string currency)
        {
            decimal total = 0;
            foreach (var (asset, usdAmount) in usdBalances)
            {
                if (currency == "USD")
                {
                    total += usdAmount;
                    continue;
                }
                //Костыль т.к. нет правила в каком порядке создавать пару
                var price = await GetPriceAsync("USD", currency);
                if (!price.HasValue)
                {
                    price = await GetPriceAsync(currency, "USD");
                    if (!price.HasValue)
                        continue;
                    total += usdAmount / price.Value;
                }
                else
                    total += usdAmount * price.Value;
            }
            return total > 0 ? new PortfolioBalance { Currency = currency, Balance = total } : null;
        }

        private async Task<decimal?> GetPriceAsync(string baseCurrency, string quoteCurrency)
        {
            var pair = $"{baseCurrency}{quoteCurrency}";
            try
            {
                var ticker = await _restClient.GetNewTradesAsync(pair, 1);
                return ticker.Any() ? ticker.First().Price : null;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
