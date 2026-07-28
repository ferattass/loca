namespace Loca.Application.Common.Logging;

/// <summary>
/// Kisisel veriyi log'a yazmadan once maskeler.
/// </summary>
/// <remarks>
/// Analiz belgesi 3.12: sifre, token ve kart bilgisi hic loglanmaz;
/// e-posta ve telefon maskelenerek yazilir. Sebep, log dosyalarinin
/// uygulamadan daha genis bir kitleye acik olmasi — hata ayiklayan herkes
/// kullanici listesini gormemeli.
/// </remarks>
public static class Masking
{
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(bos)";

        var separator = email.IndexOf('@', StringComparison.Ordinal);
        if (separator <= 0)
            return "***";

        var name = email[..separator];
        var domain = email[(separator + 1)..];
        var visible = name.Length <= 2 ? name[..1] : name[..2];

        return $"{visible}{new string('*', Math.Max(1, name.Length - visible.Length))}@{domain}";
    }

    public static string Phone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "(bos)";

        var digits = phone.Where(char.IsDigit).ToArray();
        if (digits.Length < 4)
            return "***";

        return $"***{new string(digits[^4..])}";
    }
}
