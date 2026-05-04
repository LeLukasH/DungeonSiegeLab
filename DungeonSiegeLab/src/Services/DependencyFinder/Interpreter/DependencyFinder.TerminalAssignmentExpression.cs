namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class TerminalAssignmentExpression : IAssignmentExpression
    {
        private readonly Action<DependencyInterpretContext> _interpret;

        public TerminalAssignmentExpression(Action<DependencyInterpretContext> interpret)
        {
            _interpret = interpret;
        }

        public void Interpret(DependencyInterpretContext context)
            => _interpret(context);
    }
}
