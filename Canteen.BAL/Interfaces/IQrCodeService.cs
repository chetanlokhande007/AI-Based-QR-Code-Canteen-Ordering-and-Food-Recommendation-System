namespace Canteen.BAL.Interfaces;

public interface IQrCodeService
{
    /// <summary>
    /// Constructs the frontend customer menu URL encoded into the table's QR code.
    /// </summary>
    string GenerateTableMenuUrl(int canteenId, int tableId, string qrToken);

    /// <summary>
    /// Generates a PNG byte array for the specified content string (e.g. menu URL).
    /// </summary>
    byte[] GenerateQrCodePng(string content, int pixelsPerModule = 10);
}
