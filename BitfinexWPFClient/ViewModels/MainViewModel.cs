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
        private readonly BitfinexRestClient? _restClient;
        private ObservableCollection<PortfolioBalance>? _balances;

        public ObservableCollection<PortfolioBalance> Balances
        {
            get => _balances!;
            set { _balances = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _restClient = new BitfinexRestClient();
            LoadBalancesAsync().GetAwaiter().GetResult();
        }

        private async Task LoadBalancesAsync()
        {
            var portfolio = GetInitialPortfolio();
            var usdBalances = await ConvertPortfolioToUsdAsync(portfolio);
            Balances = await CalculateBalancesInTargetCurrenciesAsync(usdBalances);
        }

        private Dictionary<string, decimal> GetInitialPortfolio()
        {
            return new Dictionary<string, decimal>
            {
                { "BTC", 1m },
                { "XRP", 15000m },
                { "XMR", 50m }
            };
        }

        private async Task<Dictionary<string, decimal>> ConvertPortfolioToUsdAsync(Dictionary<string, decimal> portfolio)
        {
            var usdBalances = new Dictionary<string, decimal>();
            foreach (var (asset, amount) in portfolio)
            {
                if (asset == "USD")
                {
                    usdBalances[asset] = amount;
                    continue;
                }
                decimal usdValue = await ConvertAssetToUsdAsync(asset, amount);
                if (usdValue > 0)
                    usdBalances[asset] = usdValue;
            }
            return usdBalances;
        }

        private async Task<decimal> ConvertAssetToUsdAsync(string asset, decimal amount)
        {
            var pair = $"{asset}USD";
            var ticker = await _restClient!.GetNewTradesAsync(pair, 1);
            if (!ticker.Any()) return 0;
            return amount * ticker.First().Price;
        }

        private async Task<ObservableCollection<PortfolioBalance>> CalculateBalancesInTargetCurrenciesAsync(
            Dictionary<string, decimal> usdBalances)
        {
            var targetCurrencies = new[] { "USD", "BTC", "XMR" };
            var balances = new ObservableCollection<PortfolioBalance>();
            foreach (var currency in targetCurrencies)
            {
                decimal total = await CalculateTotalInCurrencyAsync(usdBalances, currency);
                balances.Add(new PortfolioBalance { Currency = currency, Balance = total });
            }
            return balances;
        }

        private async Task<decimal> CalculateTotalInCurrencyAsync(
            Dictionary<string, decimal> usdBalances, string targetCurrency)
        {
            decimal total = 0;
            foreach (var (asset, usdAmount) in usdBalances)
            {
                if (targetCurrency == "USD")
                {
                    total += usdAmount;
                    continue;
                }
                decimal convertedAmount = await ConvertUsdToTargetCurrencyAsync(usdAmount, targetCurrency);
                total += convertedAmount;
            }
            return total;
        }
        private async Task<decimal> ConvertUsdToTargetCurrencyAsync(decimal usdAmount, string targetCurrency)
        {
            
            // Try USD : TargetCurrency
            var pair = $"USD{targetCurrency}";
            var ticker = await _restClient!.GetNewTradesAsync(pair, 1);
            if (ticker.Any())
            {
                return usdAmount * ticker.First().Price;
            }
            // Or TargetCurrency : USD 
            pair = $"{targetCurrency}USD";
            ticker = await _restClient.GetNewTradesAsync(pair, 1);
            if (ticker.Any())
                return usdAmount / ticker.First().Price;
            return 0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
