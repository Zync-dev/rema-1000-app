using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Rema.App.Data.Entities;

/// <summary>
/// En medarbejder-/lederkonto. Hver bruger hører til præcis én butik.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Butikken brugeren hører til.</summary>
    public Guid StoreId { get; set; }

    public Store? Store { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
