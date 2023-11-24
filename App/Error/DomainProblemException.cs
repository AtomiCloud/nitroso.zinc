namespace App.Error;

public class DomainProblemException(IDomainProblem problem) : Exception
{
  public IDomainProblem Problem { get; set; } = problem;
}
