namespace Scadix.AxamlDesign;

public interface ISplitGroupCommandService
{
    bool CanAlign { get; }
    bool CanDistribute { get; }
    bool Align(GroupAlignment alignment);
    bool Distribute(GroupDistribution direction);
}
