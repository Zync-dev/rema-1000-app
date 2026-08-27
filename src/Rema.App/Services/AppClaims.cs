namespace Rema.App.Services;

/// <summary>Navne på egne claims der lægges på den indloggede bruger.</summary>
public static class AppClaims
{
    /// <summary>Id på brugerens butik (<c>Store.Id</c>) som streng.</summary>
    public const string StoreId = "rema:store_id";

    /// <summary>Butikkens visningsnavn – bekvemt til UI uden et databasekald.</summary>
    public const string StoreName = "rema:store_name";
}
