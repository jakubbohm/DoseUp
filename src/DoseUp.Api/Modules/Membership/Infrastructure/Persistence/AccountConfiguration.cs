using DoseUp.Api.Modules.Membership.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoseUp.Api.Modules.Membership.Infrastructure.Persistence;

/// <summary>
/// Account mapping. Identifiers are left to the snake_case naming convention wherever
/// EF generates them (spike #93); requiredness is left to NRT inference — never restated
/// with <c>IsRequired()</c> (conventions § Persistence); ids are client-minted v7 uuids,
/// never store-generated.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account> {
  public void Configure(EntityTypeBuilder<Account> builder) {
    builder.HasKey(static account => account.Id);
    builder.Property(static account => account.Id).ValueGeneratedNever();

    // ix_accounts_entra_object_id (generated, deterministic, snake_case): the ring-1
    // lookup path AND the set-rule backstop for "one account per identity" — the future
    // signup slice maps the DbUpdateException by this name (domain-rules.md §6). The
    // index call is also what maps the get-only EntraObjectId property.
    builder.HasIndex(static account => account.EntraObjectId).IsUnique();

    // Get-only (mutation not modeled yet / born-immutable) ⇒ skipped by convention
    // discovery: these calls ARE the mapping, not requiredness restatements.
    builder.Property(static account => account.DisplayName);
    builder.Property(static account => account.Email);
    builder.Property(static account => account.CreatedAt);
  }
}
