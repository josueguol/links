using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Links.Api.Modules.Auth;

public sealed class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySize = 65536;   // 64 MiB
    private const int Iterations = 3;
    private const int Parallelism = 4;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = GetHash(password, salt);

        // Self-contained format: $argon2id$v=19$m=65536,t=3,p=4$<salt>$<hash>
        return string.Join("$",
            "$argon2id$v=19",
            $"m={MemorySize},t={Iterations},p={Parallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 6)
            return false;

        var salt = Convert.FromBase64String(parts[4]);
        var storedHash = Convert.FromBase64String(parts[5]);
        var computedHash = GetHash(password, salt);

        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }

    private static byte[] GetHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = MemorySize;
        argon2.Iterations = Iterations;
        argon2.DegreeOfParallelism = Parallelism;
        return argon2.GetBytes(HashSize);
    }
}
