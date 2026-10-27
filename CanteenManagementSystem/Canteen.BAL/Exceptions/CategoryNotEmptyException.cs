using System;

namespace Canteen.BAL.Exceptions;

/// <summary>
/// Thrown when attempting to delete a category that still has associated products.
/// </summary>
public class CategoryNotEmptyException : Exception
{
    public CategoryNotEmptyException(string message)
        : base(message)
    {
    }

    public CategoryNotEmptyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
