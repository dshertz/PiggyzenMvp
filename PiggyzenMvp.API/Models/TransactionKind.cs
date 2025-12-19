namespace PiggyzenMvp.API.Models;

public enum TransactionKind
{
    Unknown = 0,
    CardPurchase,
    Swish,
    Payment,
    Fee,
    Transfer,
    Deposit,
    LoanPayment,
    Interest,
    Adjustment,
}
