using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Rema.App.Data;

/// <summary>
/// Genererer tidsordnede UUID v7-nøgler klientside ved indsættelse. Giver bedre
/// index-lokalitet end tilfældige v4-GUID'er, og – i modsætning til en
/// CLR-feltinitializer – forvirrer den ikke EF's Added/Modified-registrering.
/// </summary>
public sealed class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
}
