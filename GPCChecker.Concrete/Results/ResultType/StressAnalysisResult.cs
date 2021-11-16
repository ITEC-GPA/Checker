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


        public ResultBeamForces Force => _force;

        public StrainPlane StrainPlane => _strainPlane;


        public StressAnalysisResult(IConcreteSection section, ResultBeamForces force, StrainPlane strainPlane, Standard standard, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _force = force ?? throw new ArgumentNullException(nameof(force));
            _strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
        }


        public virtual double GetConcreteTension(Point3d point)
        {
            throw new NotImplementedException();
        }

        public virtual double[] GetVerticesTension()
        {
            return _section.Shape.Fill.Select(i => GetConcreteTension(i)).ToArray();
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
                strains[i] = GetStrain(vertices[i]);

            return strains;
        }

    }
}
