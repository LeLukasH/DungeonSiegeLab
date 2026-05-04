namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class NonterminalAssignmentExpression : IAssignmentExpression
    {
        private readonly IReadOnlyList<IAssignmentExpression> _children;

        public NonterminalAssignmentExpression(params IAssignmentExpression[] children)
        {
            _children = children;
        }

        public void Interpret(DependencyInterpretContext context)
        {
            foreach (var child in _children)
                child.Interpret(context);
        }
    }
}
