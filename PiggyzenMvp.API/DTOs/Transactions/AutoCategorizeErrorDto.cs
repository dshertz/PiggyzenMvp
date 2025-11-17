namespace PiggyzenMvp.API.DTOs;

public class AutoCategorizeErrorDto
{
    public int TransactionId { get; set; }
    public string Message { get; set; } = string.Empty;
}
