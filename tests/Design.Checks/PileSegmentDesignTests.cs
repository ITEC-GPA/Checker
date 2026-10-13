using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Design.Piles;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DesignTests;
[TestClass]
public sealed class PileSegmentDesignTests
{
    static PileRcParameters Factory(PileDesignLayout layout)
    {
        var concrete=new ConcreteMaterialEN1992("C35",35,ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel=new SteelMaterial("B450",200000,450,450,.1,SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic,SteelMaterial.SteelTypes.Rebar);
        var section=new ReinforcedConcreteSection(new Shape2d(new Polygon2d(1000,64)),concrete);
        var perimeter=new Polygon2d(1000-2*(70+layout.LinkDiameter+layout.Diameter/2),layout.Count);
        var rebar=new RebarSectionCircular(layout.Diameter,steel);
        section.AddRebars(Enumerable.Range(0,layout.Count).Select(i=>new ReinforcedConcreteRebar(rebar,perimeter[i])));
        var axes=new CoordinateSystem(section.Centroid,new Vector3d(-1,0,0),new Vector3d(0,-1,0));var standard=new StandardNTC2018Concrete();
        var options=new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes,SectionSolver.FailureAnalysisTypes.ConstantN,
            SectionSolver.FailureDomainTypes.Plastic,SectionSolver.StressAnalysisTypes.NonLinear,0,0,false,32);
        var checker=new SectionCheckerModelCode2010(new SectionCheckerAttribute(section,null,null),options,standard,false);
        checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
        return new PileRcParameters {Solver=checker.SectionSolver,Axes=axes,Section=section,Standard=standard,Diameter=1000,Fck=35,Fctm=concrete.Fctm,
            Fctk05=concrete.Fctk05,Fcd=Math.Abs(concrete.CalculateFcd(standard)),Fyd=Math.Abs(steel.CalculateFyd(standard)),Es=200000,
            Cover=70,LinkDiameter=layout.LinkDiameter,LinkSpacing=layout.LinkSpacing,ShearModelConfirmed=true,DesignActionsConfirmed=true,GoodBond=true};
    }
    static PileAction[] Actions()=>[new(){Depth=1,N=1000,M=100,V=100},new(){Depth=5,N=500,M=-50,V=-30},new(){Depth=11,N=1e8,M=1e8,V=1e8}];
    static PileSegmentDesignResult Search(int workers,Func<PileDesignLayout,PileRcParameters>? factory=null,CancellationToken token=default,
        IEnumerable<PileAction>? actions=null,IProgress<PileDesignProgress>? progress=null)=>
        PileSegmentDesign.Search([16],[24],[10],[150],actions??Actions(),0,10,12,new PileSeismicSettings(),factory??Factory,token,progress,workers);
    [TestMethod]
    public void SequentialAndParallelSelectTheSameVerifiedLayoutWithoutChangingActions()
    {
        var actions=Actions();var snapshot=actions.Select(a=>(a.Depth,a.N,a.V,a.M)).ToArray();
        var sequential=Search(1,actions:actions);var parallel=Search(2,actions:actions);
        Assert.IsNotNull(sequential.Selected);Assert.AreEqual(new PileDesignLayout(16,24,10,150),sequential.Selected);
        Assert.AreEqual(sequential.Selected,parallel.Selected);Assert.AreEqual(1,sequential.Evaluated);Assert.AreEqual(1,parallel.Evaluated);
        CollectionAssert.AreEqual(snapshot,actions.Select(a=>(a.Depth,a.N,a.V,a.M)).ToArray());
        Assert.AreEqual(0,PileReinforcement.SelectCandidate([Factory(sequential.Selected!)],actions.Take(2).ToArray()));
    }
    [TestMethod]
    public void InvalidMissingAndCancelledInputCannotReturnASolution()
    {
        int called=0;PileRcParameters Invalid(PileDesignLayout _){called++;throw new InvalidOperationException("Factory must not run");}
        Assert.ThrowsException<ArgumentException>(()=>Search(1,Invalid,actions:[]));
        Assert.ThrowsException<ArgumentException>(()=>Search(1,Invalid,actions:[new(){Depth=1,N=double.NaN}]));
        Assert.ThrowsException<OperationCanceledException>(()=>Search(1,Invalid,new CancellationToken(true)));
        Assert.AreEqual(0,called);
    }
    [TestMethod]
    public void GeometricRefusalsAndCancellationDuringSearchStayDistinct()
    {
        var rejected=Search(1,_=>throw new ArgumentException("Rebars overlap"));
        Assert.IsNull(rejected.Selected);Assert.AreEqual(1,rejected.Evaluated);Assert.AreEqual("Rebars overlap",rejected.GeometricExclusions.Single().Diagnostic);
        using var cancellation=new CancellationTokenSource();
        Assert.ThrowsException<OperationCanceledException>(()=>Search(1,l=>{cancellation.Cancel();return Factory(l);},cancellation.Token));
    }
}
