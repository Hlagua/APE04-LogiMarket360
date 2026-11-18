using System.Security.Cryptography;
using LogiMarket.Application.Seguridad;

namespace LogiMarket.Infrastructure.Seguridad;

/// <summary>Hash PBKDF2-SHA256 con sal aleatoria. Formato: PBKDF2$iteraciones$salBase64$hashBase64.</summary>
public sealed class HasherContrasenaPbkdf2 : IHasherContrasena
{
    private const int Iteraciones = 100_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;

    public string Hashear(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return $"PBKDF2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string contrasena, string hashAlmacenado)
    {
        var partes = hashAlmacenado.Split('$');
        if (partes.Length != 4 || partes[0] != "PBKDF2" || !int.TryParse(partes[1], out var iteraciones)) return false;
        var sal = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
