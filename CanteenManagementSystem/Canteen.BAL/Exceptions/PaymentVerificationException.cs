using System;

namespace Canteen.BAL.Exceptions;

/// <summary>
/// Thrown when payment signature verification fails or payment details do not match the expected state.
/// </summary>
public class PaymentVerificationException : Exception
{
    public PaymentVerificationException(string message)
        : base(message)
    {
    }

    public PaymentVerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
