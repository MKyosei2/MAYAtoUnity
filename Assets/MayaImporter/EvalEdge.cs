namespace MayaImporter.Phase3.Evaluation
{
    public class EvalEdge
    {
        public EvalNode From;
        public EvalNode To;

        public EvalEdge(EvalNode from, EvalNode to)
        {
            From = from;
            To = to;
        }
    }
}
