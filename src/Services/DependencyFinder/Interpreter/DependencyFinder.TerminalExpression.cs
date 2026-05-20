namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private abstract class TerminalExpression : IExpression
    {
        public abstract void Interpret(DependencyInterpretContext context);
    }
}
