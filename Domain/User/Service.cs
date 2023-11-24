using CSharp_Result;

namespace Domain.User;

public class UserService(IUserRepository repo) : IUserService
{
  public Task<Result<IEnumerable<UserPrincipal>>> Search(UserSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<User?>> GetById(string id)
  {
    return repo.GetById(id);
  }

  public Task<Result<User?>> GetByUsername(string username)
  {
    return repo.GetByUsername(username);
  }

  public Task<Result<bool>> Exists(string username)
  {
    return repo.Exists(username);
  }

  public Task<Result<UserPrincipal>> Create(string id, UserRecord record)
  {
    return repo.Create(id, record);
  }

  public Task<Result<UserPrincipal?>> Update(string id, UserRecord record)
  {
    return repo.Update(id, record);
  }

  public Task<Result<Unit?>> Delete(string id)
  {
    return repo.Delete(id);
  }
}
