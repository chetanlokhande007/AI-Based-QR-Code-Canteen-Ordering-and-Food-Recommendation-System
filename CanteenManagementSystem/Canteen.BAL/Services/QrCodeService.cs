using System;
using Canteen.BAL.Interfaces;
using Microsoft.Extensions.Configuration;
using QRCoder;

namespace Canteen.BAL.Services;

public class QrCodeService : IQrCodeService
{
    private readonly string _baseMenuUrl;

    public QrCodeService(IConfiguration configuration)
    {
        _baseMenuUrl = configuration["Frontend:BaseMenuUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200/menu";
    }

    public string GenerateTableMenuUrl(int canteenId, int tableId, string qrToken)
    {
        return $"{_baseMenuUrl}?canteenId={canteenId}&tableId={tableId}&token={Uri.EscapeDataString(qrToken)}";
    }

    public byte[] GenerateQrCodePng(string content, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("QR content cannot be null or empty.", nameof(content));
        }

        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
