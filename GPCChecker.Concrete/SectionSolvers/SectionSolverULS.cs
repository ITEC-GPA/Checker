using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.Model.Materials;
using GPC.Model.Maths.GaussIntegrations;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;
using MathNet.Numerics.LinearAlgebra;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    public class SectionSolverULS : SectionSolverModelCode2010
    {



        public SectionSolverULS(IConcreteSection section, Standard standard)
            : base(section, standard)
        {

        }

        public SectionSolverULS(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

    }
}
