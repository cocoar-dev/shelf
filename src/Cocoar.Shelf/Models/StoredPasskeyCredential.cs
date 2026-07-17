namespace Cocoar.Shelf.Models;

public class StoredPasskeyCredential
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] CredentialId { get; set; } = [];

    public byte[] PublicKey { get; set; } = [];

    public uint SignatureCounter { get; set; }

    public string? DisplayName { get; set; }

    public Guid UserHandle { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}
