using CSharp_Result;

namespace Domain.User;

public interface IUserService
{
  Task<Result<IEnumerable<UserPrincipal>>> Search(UserSearch search);

  Task<Result<User?>> GetById(string id);
  Task<Result<User?>> GetByUsername(string username);

  Task<Result<bool>> Exists(string username);

  Task<Result<UserPrincipal>> Create(string id, UserRecord record);
  Task<Result<UserPrincipal?>> Update(string id, UserRecord record);

  // admin-managed ExtraRoles (pricing/discount targeting only)
  // add is idempotent; null when no such user exists
  Task<Result<UserPrincipal?>> AddExtraRole(string id, string role);

  // null when no such user exists OR the user does not have the role
  Task<Result<UserPrincipal?>> RemoveExtraRole(string id, string role);

  Task<Result<Unit?>> Delete(string id);
}
