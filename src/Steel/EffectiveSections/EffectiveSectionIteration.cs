using System;
using System.Threading;
using GPC.Checkers.Steel.Inputs;

namespace GPC.Checkers.Steel.EffectiveSections
{
    public sealed class EffectiveSectionConvergenceException : InvalidOperationException
    {
        public int Iterations { get; }
        internal EffectiveSectionConvergenceException(int iterations)
            : base("Effective section did not converge after "+iterations+" iterations. No usable result.") { Iterations=iterations; }
    }
    public sealed class EffectiveIterationResult<TState,TResponse>
    {
        public TState State { get; }
        public TResponse Response { get; }
        public int Iterations { get; }
        public double Residual { get; }
        internal EffectiveIterationResult(TState state,TResponse response,int iterations,double residual)
        {State=state;Response=response;Iterations=iterations;Residual=residual;}
    }
    /// <summary>Geometry-neutral fixed point iteration. On convergence, stresses/response
    /// belong to the returned state, never to the unevaluated next geometry. No pass on failure.</summary>
    public static class EffectiveSectionIteration
    {
        public const double MinimumRelaxation=.05;
        public static EffectiveIterationResult<TState,TResponse> Solve<TState,TResponse>(TState initial,
            Func<TState,TResponse> evaluate,Func<TResponse,TState> reduce,Func<TState,TState,double> distance,
            Func<TState,TState,double,TState> relax,int maximumIterations=120,double tolerance=1e-7,double relaxation=.55,
            Func<TState,double[]> coordinates=null,CancellationToken cancellation=default)
        {
            if(evaluate==null||reduce==null||distance==null||relax==null)throw new ArgumentNullException("iteration adapter");
            if(maximumIterations<1)throw new ArgumentOutOfRangeException(nameof(maximumIterations));
            SteelInputValidation.Positive(tolerance,nameof(tolerance));SteelInputValidation.Positive(relaxation,nameof(relaxation));
            if(relaxation>1)throw new ArgumentOutOfRangeException(nameof(relaxation));
            var state=initial;double factor=relaxation;double[] lastResidual=null;
            for(int iteration=1;iteration<=maximumIterations;iteration++) {
                cancellation.ThrowIfCancellationRequested();
                var response=evaluate(state);var next=reduce(response);double error=distance(state,next);
                if(double.IsNaN(error)||double.IsInfinity(error)||error<0)throw new InvalidOperationException("Invalid effective-section residual.");
                if(error<tolerance)return new EffectiveIterationResult<TState,TResponse>(state,response,iteration,error);
                if(coordinates!=null) {
                    double[] a=coordinates(state),b=coordinates(next);
                    if(a==null||b==null||a.Length==0||a.Length!=b.Length||lastResidual!=null&&lastResidual.Length!=a.Length)
                        throw new InvalidOperationException("Effective-section coordinate dimensions changed.");
                    var residual=new double[a.Length];
                    for(int j=0;j<a.Length;j++){residual[j]=b[j]-a[j];SteelInputValidation.Finite(residual[j],"iteration coordinate residual");}
                    if(lastResidual!=null) {
                        double numerator=0,denominator=0;
                        for(int j=0;j<a.Length;j++) {double d=residual[j]-lastResidual[j];numerator+=lastResidual[j]*d;denominator+=d*d;}
                        double candidate=-factor*numerator/denominator;
                        if(denominator>0&&!double.IsNaN(candidate)&&!double.IsInfinity(candidate))factor=Math.Max(MinimumRelaxation,Math.Min(1,candidate));
                    }
                    lastResidual=residual;
                }
                state=relax(state,next,factor);
            }
            throw new EffectiveSectionConvergenceException(maximumIterations);
        }
    }
}
