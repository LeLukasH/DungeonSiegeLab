namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private interface IExpression
    {
        void Interpret(DependencyInterpretContext context);
    }
}
