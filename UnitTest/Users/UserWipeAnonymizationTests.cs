using App.Modules.Users.Data;

namespace UnitTest.Users;

// The row-level anonymization shape: what a PDPA wipe blanks and what it
// deliberately keeps on the Users row (the id survives for financial FK
// linkage; everything identifying goes).
public class UserWipeAnonymizationTests
{
  private static readonly DateTime WipeStamp = new(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);

  private static UserData Original() =>
    new()
    {
      Id = "user-1234567890",
      Username = "bunny",
      Email = "bunny@example.com",
      EmailVerified = true,
      Roles = ["role_a"],
      ExtraRoles = ["partner"],
    };

  [Fact]
  public void ApplyWipe_blanks_every_identifying_field()
  {
    var data = UserRepository.ApplyWipe(Original(), "admin-1", WipeStamp);

    data.Username.Should().Be("deleted-user-123");
    data.Email.Should().Be("");
    data.EmailVerified.Should().Be(false);
    // Roles mirror the (now dead) Descope JWT; ExtraRoles drive the partner
    // listing and pricing — both cleared so nothing targets a ghost
    data.Roles.Should().BeNull();
    data.ExtraRoles.Should().BeEmpty();
  }

  [Fact]
  public void ApplyWipe_keeps_the_id_and_stamps_the_audit_fields()
  {
    var data = UserRepository.ApplyWipe(Original(), "admin-1", WipeStamp);

    data.Id.Should().Be("user-1234567890");
    data.WipedAt.Should().Be(WipeStamp);
    data.WipedById.Should().Be("admin-1");
  }
}
