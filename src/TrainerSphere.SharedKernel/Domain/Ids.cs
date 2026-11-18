using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace TrainerSphere.SharedKernel.Domain;

public static class Ids
{
    public static Guid New(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return Guid.CreateVersion7(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Creates a name-based UUID (RFC 9562 version 5): the same namespace and name always give the same id, in
    /// every environment.
    /// </summary>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "Version 5 UUIDs are defined over SHA-1; the hash is an identifier, not a security control.")]
    [SuppressMessage("Security", "S4790:Using weak hashing algorithms is security-sensitive", Justification = "Version 5 UUIDs are defined over SHA-1; the hash is an identifier, not a security control.")]
    public static Guid FromName(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] input = new byte[16 + nameBytes.Length];
        namespaceId.TryWriteBytes(input, bigEndian: true, out _);
        nameBytes.CopyTo(input, 16);

        byte[] hash = SHA1.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
