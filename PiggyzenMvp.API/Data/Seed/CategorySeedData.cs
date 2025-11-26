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
            new(1, "salary", "Salary", 1),
            new(1, "pension", "Pension", 2),
            new(1, "sick-pay-parental-pay", "Sick Pay & Parental Pay", 3),
            new(
                1,
                "disability-benefit-activity-allowance",
                "Disability Benefit & Activity Allowance",
                4
            ),
            new(1, "unemployment-benefit", "Unemployment Benefit", 5),
            new(1, "care-allowance", "Care Allowance", 6),
            new(1, "child-benefit", "Child Benefit", 7),
            new(1, "housing-benefit-supplement", "Housing Benefit & Housing Supplement", 8),
            new(1, "student-grant", "Student Grant", 9),
            new(1, "maintenance-support", "Maintenance Support", 10),
            new(1, "capital-income", "Capital Income", 11),
            new(1, "other-income", "Other Income", 12),

            // Housing
            new(2, "rent-fee", "Rent & Fee", 1),
            new(2, "housing-utilities", "Housing Utilities", 2),
            new(2, "home-insurance", "Home Insurance", 3),
            new(2, "mortgage", "Mortgage", 4),
            new(2, "other-housing", "Other Housing", 5),

            // Vehicle
            new(3, "fuel", "Fuel", 1),
            new(3, "vehicle-running-costs", "Vehicle Running Costs", 2),
            new(3, "vehicle-insurance", "Vehicle Insurance", 3),
            new(3, "car-loan", "Car Loan", 4),
            new(3, "other-vehicle", "Other Vehicle", 5),

            // Fixed Expenses
            new(4, "subscriptions-services", "Subscriptions & Services", 1),
            new(4, "other-insurance", "Other Insurance", 2),
            new(4, "childcare", "Childcare", 3),
            new(4, "union-unemployment-fees", "Union & Unemployment Fees", 4),
            new(4, "student-loan", "Student Loan", 5),
            new(4, "other-loans", "Other Loans", 6),
            new(4, "other-fixed-expenses", "Other Fixed Expenses", 7),

            // Variable Expenses
            new(5, "food-drinks-other", "Food & Drinks Other", 1),
            new(5, "groceries", "Groceries", 2),
            new(5, "restaurants", "Restaurants", 3),
            new(5, "alcohol", "Alcohol", 4),
            new(5, "shopping", "Shopping", 5),
            new(5, "home-garden", "Home & Garden", 6),
            new(5, "leisure-entertainment", "Leisure & Entertainment", 7),
            new(5, "transport", "Transport", 8),
            new(5, "health-care", "Health & Care", 9),
            new(5, "other-variable-expenses", "Other Variable Expenses", 10),

            // Transfers
            new(6, "advances-loans", "Advances & Loans", 1),
            new(6, "refunds", "Refunds", 2),
            new(6, "savings", "Savings", 3),
            new(6, "internal-transfers", "Internal Transfers", 4),
            new(6, "cash-withdrawal", "Cash Withdrawal", 5),
            new(6, "other-transfers", "Other Transfers", 6),
        };
}

public record CategoryGroupSeed(int Id, string Key, string DisplayName, int SortOrder);

public record CategorySeed(int GroupId, string Key, string DisplayName, int SortOrder);
