using CSharp_Result;

namespace Domain.User;

public interface IUserRepository
{
  Task<Result<IEnumerable<UserPrincipal>>> Search(UserSearch search);

  Task<Result<User?>> GetById(string id);

  Task<Result<User?>> GetByUsername(string username);

  Task<Result<bool>> Exists(string username);

  Task<Result<UserPrincipal>> Create(string id, UserRecord record);

  Task<Result<UserPrincipal?>> Update(string id, UserRecord record);

  Task<Result<Unit?>> Delete(string id);
}
