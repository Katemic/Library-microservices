namespace Loan.Models;

/// <summary>Loan rules, bound from the "Loan" config section.</summary>
public class LoanOptions
{
    /// <summary>How many days a loan lasts before it is due.</summary>
    public int LoanPeriodDays { get; set; } = 30;
}
