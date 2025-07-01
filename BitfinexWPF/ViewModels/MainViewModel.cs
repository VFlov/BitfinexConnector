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
            var balances = new ObservableCollection<PortfolioBalance>();
            var usdBalances = new Dictionary<string, decimal>();

            // Convert all assets to USD
            foreach (var (asset, amount) in portfolio)
            {
                if (asset == "USD")
                {
                    usdBalances[asset] = amount;
                    continue;
                }

                var pair = $"{asset}USD";
                var ticker = await _restClient.GetNewTradesAsync(pair, 1);
                if (!ticker.Any())
                    continue;

                var price = ticker.First().Price;
                usdBalances[asset] = amount * price;
            }

            // Convert USD to target currencies
            foreach (var currency in new[] { "USD", "BTC", "XMR" })
            {
                decimal total = 0;
                foreach (var (asset, usdAmount) in usdBalances)
                {
                    if (currency == "USD")
                    {
                        total += usdAmount;
                        continue;
                    }

                    var pair = $"USD{currency}";
                    var ticker = await _restClient.GetNewTradesAsync(pair, 1);
                    if (!ticker.Any())
                    {
                        pair = $"{currency}USD";
                        ticker = await _restClient.GetNewTradesAsync(pair, 1);
                        if (!ticker.Any())
                            continue;
                        total += usdAmount / ticker.First().Price;
                    }
                    else
                    {
                        total += usdAmount * ticker.First().Price;
                    }
                }
                balances.Add(new PortfolioBalance { Currency = currency, Balance = total });
            }

            Balances = balances;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
