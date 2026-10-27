using System;

namespace Canteen.BAL.Exceptions;

/// <summary>
/// Thrown when an order contains invalid business data, cross-canteen product mismatch, or illegal status transitions.
/// </summary>
public class InvalidOrderException : Exception
{
    public InvalidOrderException(string message)
        : base(message)
    {
    }

    public InvalidOrderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
