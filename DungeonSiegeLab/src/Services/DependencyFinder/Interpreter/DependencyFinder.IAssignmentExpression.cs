namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private interface IAssignmentExpression
    {
        void Interpret(DependencyInterpretContext context);
    }
}
