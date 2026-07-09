using CSharp_Result;

namespace Domain.Milestone;

public interface IMilestoneRepository
{
  // all milestones, newest Date first
  Task<Result<IEnumerable<MilestonePrincipal>>> List();

  Task<Result<MilestonePrincipal>> Add(MilestoneRecord record);

  // null when the milestone does not exist
  Task<Result<MilestonePrincipal?>> Delete(Guid id);
}
