using Microsoft.AspNetCore.Identity;

namespace Boutique.Domain;

// Extends IdentityUser<int> so Ids stay plain ints, matching the rest of your schema
public class AdminUser : IdentityUser<int>
{
    public string DisplayName { get; set; } = default!;
}