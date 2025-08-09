using CSharp_Result;

namespace App.StartUp.Email;

public interface IEmailRenderer
{
    Task<Result<string>> RenderEmail(string id, object variables);
}
