namespace PiggyzenMvp.API.Data.Seed;

public static class CategorySeedData
{
    public static IReadOnlyList<CategoryGroupSeed> Groups { get; } =
        new List<CategoryGroupSeed>
        {
            new(1, "income", "Income", 1),
            new(2, "housing", "Housing", 2),
            new(3, "vehicle", "Vehicle", 3),
            new(4, "fixed-expenses", "Fixed Expenses", 4),
            new(5, "variable-expenses", "Variable Expenses", 5),
            new(6, "transfers", "Transfers", 6),
        };

    public static IReadOnlyList<CategorySeed> Categories { get; } =
        new List<CategorySeed>
        {
            // Income
            new(1, "salary", "Salary", 1, "Lön"),
            new(1, "pension", "Pension", 2, "Pension"),
            new(1, "sick-pay-parental-pay", "Sick Pay & Parental Pay", 3, "Sjukpenning & Föräldrapenning"),
            new(
                1,
                "disability-benefit-activity-allowance",
                "Disability Benefit & Activity Allowance",
                4,
                "Sjukersättning & Aktivitetsersättning"
            ),
            new(1, "unemployment-benefit", "Unemployment Benefit", 5, "Arbetslöshetsersättning"),
            new(1, "care-allowance", "Care Allowance", 6, "Vårdbidrag & Omvårdnadsbidrag"),
            new(1, "child-benefit", "Child Benefit", 7, "Barnbidrag"),
            new(
                1,
                "housing-benefit-supplement",
                "Housing Benefit & Housing Supplement",
                8,
                "Bostadsbidrag & Bostadstillägg"
            ),
            new(1, "student-grant", "Student Grant", 9, "Studiestöd"),
            new(1, "maintenance-support", "Maintenance Support", 10, "Underhållsbidrag"),
            new(1, "capital-income", "Capital Income", 11, "Kapitalinkomster"),
            new(1, "other-income", "Other Income", 12, "Övriga inkomster"),

            // Housing
            new(2, "rent-fee", "Rent & Fee", 1, "Hyra & Avgift"),
            new(2, "housing-utilities", "Housing Utilities", 2, "Boendedrift"),
            new(2, "home-insurance", "Home Insurance", 3, "Boendeförsäkring"),
            new(2, "mortgage", "Mortgage", 4, "Bolån"),
            new(2, "other-housing", "Other Housing", 5, "Boende övrigt"),

            // Vehicle
            new(3, "fuel", "Fuel", 1, "Bränsle"),
            new(3, "vehicle-running-costs", "Vehicle Running Costs", 2, "Fordonsdrift"),
            new(3, "vehicle-insurance", "Vehicle Insurance", 3, "Fordonsförsäkring"),
            new(3, "car-loan", "Car Loan", 4, "Fordonslån"),
            new(3, "other-vehicle", "Other Vehicle", 5, "Fordon övrigt"),

            // Fixed Expenses
            new(4, "subscriptions-services", "Subscriptions & Services", 1, "Abonnemang & Tjänster"),
            new(4, "other-insurance", "Other Insurance", 2, "Övriga försäkringar"),
            new(4, "childcare", "Childcare", 3, "Barnomsorg"),
            new(4, "union-unemployment-fees", "Union & Unemployment Fees", 4, "A-kassa & Fackavgift"),
            new(4, "student-loan", "Student Loan", 5, "Studielån"),
            new(4, "other-loans", "Other Loans", 6, "Övriga lån"),
            new(4, "other-fixed-expenses", "Other Fixed Expenses", 7, "Fasta utgifter övrigt"),

            // Variable Expenses
            new(5, "food-drinks-other", "Food & Drinks Other", 1, "Mat & Dryck övrigt"),
            new(5, "groceries", "Groceries", 2, "Livsmedel"),
            new(5, "restaurants", "Restaurants", 3, "Restaurang"),
            new(5, "alcohol", "Alcohol", 4, "Alkohol"),
            new(5, "shopping", "Shopping", 5, "Shopping"),
            new(5, "home-garden", "Home & Garden", 6, "Hem & Trädgård"),
            new(5, "leisure-entertainment", "Leisure & Entertainment", 7, "Fritid & Nöje"),
            new(5, "transport", "Transport", 8, "Transport"),
            new(5, "health-care", "Health & Care", 9, "Hälsa & Vård"),
            new(5, "other-variable-expenses", "Other Variable Expenses", 10, "Rörliga utgifter övrigt"),

            // Transfers
            new(6, "advances-loans", "Advances & Loans", 1, "Utlägg & Lån"),
            new(6, "refunds", "Refunds", 2, "Återbetalningar"),
            new(6, "savings", "Savings", 3, "Sparande"),
            new(6, "internal-transfers", "Internal Transfers", 4, "Överföringar egna konton"),
            new(6, "cash-withdrawal", "Cash Withdrawal", 5, "Kontantuttag"),
            new(6, "other-transfers", "Other Transfers", 6, "Överföringar övrigt"),
        };
}

public record CategoryGroupSeed(int Id, string Key, string DisplayName, int SortOrder);

public record CategorySeed(int GroupId, string Key, string DisplayName, int SortOrder, string? DefaultUserDisplayName);
