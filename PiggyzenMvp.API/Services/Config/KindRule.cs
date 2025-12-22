using System.Collections.Generic;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Services.Config;

public sealed class KindRule
{
    public KindRule(TransactionKind kind, IReadOnlyList<string> keywords)
    {
        Kind = kind;
        Keywords = keywords;
    }

    public TransactionKind Kind { get; }
    public IReadOnlyList<string> Keywords { get; }
}
