namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class NonterminalExpression : IExpression
    {
        private readonly IReadOnlyList<IExpression> _children;

        public NonterminalExpression(params IExpression[] children)
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
