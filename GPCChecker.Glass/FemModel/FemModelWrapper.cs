using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Geometry;

namespace GPC.Checker.Glasses.FemModel
{
    public class FemModelWrapper
    {
        private List<GlassFemNode> _nodes;
        private List<GlassFemPlate> _plates;

        public FemModelWrapper(List<Mesh> meshes)
        {
            _ = meshes ?? throw new ArgumentNullException(nameof(meshes));
        }
        


        private sealed class GlassFemNode 
        {

        }

        private sealed class GlassFemPlate 
        {

        }
    }
}
