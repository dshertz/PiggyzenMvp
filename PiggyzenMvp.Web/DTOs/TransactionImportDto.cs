using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PiggyzenMvp.Web.DTOs
{
    public class TransactionImportDto
    {
        public DateTime? BookingDate { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = "";
        public string NormalizedDescription { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal? Balance { get; set; }
        public string ImportId { get; set; } = "";
    }
}
