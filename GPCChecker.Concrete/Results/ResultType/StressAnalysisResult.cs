using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class StressAnalysisResult : CheckerResultType, ISerializable
    {

        protected readonly ResultBeamForces _force;
        protected readonly StrainPlane _strainPlane;
        protected readonly SectionSolver _sectionSolver;

        public ResultBeamForces Force => _force;

        public StrainPlane StrainPlane => _strainPlane;


        public StressAnalysisResult(IConcreteSection section, ResultBeamForces force, StrainPlane strainPlane, SectionSolver solver, Standard standard, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _force = force ?? throw new ArgumentNullException(nameof(force));
            _strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
            _sectionSolver = solver;
        }


        public virtual double GetConcreteTension(Point2d point)
        {
            return _sectionSolver.CalculateSigmaC(StrainPlane.GetStrain(point));   
        }

        public virtual (Point2d point, double tension)[] GetConcreteVerticesTension(Point2d point)
        {
            return _section.Shape.GetPoints2d().Select(i => (i, _sectionSolver.CalculateSigmaC(StrainPlane.GetStrain(i)))).ToArray();
        }

        public virtual double GetRebarTension(ReinforcedConcreteRebar rebar)
        {            
            return _sectionSolver.CalculateStressRebar(rebar, StrainPlane.GetStrain(rebar.Position));
        }

        public virtual (ReinforcedConcreteRebar rebar, double tension)[] GetRebarsTension()
        {
            return _section.Rebars.Select(i => (i, _sectionSolver.CalculateStressRebar(i, StrainPlane.GetStrain(i.Position)) )  ).ToArray();
        }


        public double[] GetVerticesStrain()
        {
            List<Point3d> vertices = new List<Point3d>();

            vertices.AddRange(ConcreteSection.Shape.Fill);

            if (ConcreteSection.Shape.HasHoles)
                for (int i = 0; i < ConcreteSection.Shape.Holes.Count(); i++)
                    vertices.AddRange(ConcreteSection.Shape.Holes[i]);

            double[] strains = new double[vertices.Count];

            for (int i = 0; i < strains.Length; i++)
                strains[i] = _strainPlane.GetStrain(vertices[i]);

            return strains;
        }

    }
}
