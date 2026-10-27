namespace Loan.Messaging;

public class RabbitMqOptions
{
    public string Uri { get; set; } = string.Empty;

    public string Exchange { get; set; } = "loan.events";
}