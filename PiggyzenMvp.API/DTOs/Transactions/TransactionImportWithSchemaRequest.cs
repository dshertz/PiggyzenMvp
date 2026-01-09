namespace PiggyzenMvp.API.DTOs;

public class TransactionImportWithSchemaRequest
{
    public required string RawText { get; set; }
    public required TransactionImportSchemaDefinition Schema { get; set; }
}
