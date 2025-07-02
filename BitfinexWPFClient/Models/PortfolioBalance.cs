using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitfinexWPF.Models
{
    public class PortfolioBalance
    {
        public string? Currency { get; set; }
        public decimal Balance { get; set; }
    }
}
