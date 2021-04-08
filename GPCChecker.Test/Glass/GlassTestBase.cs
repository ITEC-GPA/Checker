using GPC.Geometry;
using GPC.Geometry.Meshes;
using GPC.TestUtilities;
using System.Collections.Generic;

namespace GlassTests
{
    public abstract class GlassTestBase : UnitTestBase
    {
        public Shape GetRectangularShape(double width, double height)
        {
            Polygon3d p = new Polygon3d()
            {
                new Point3d(0,          0,      0),
                new Point3d(width,      0,      0),
                new Point3d(width, height,      0),
                new Point3d(0,     height,      0)
            };

            return new Shape(p);
        }

        public Shape GetRectangularShape(Point3d p, Vector3d vector)
        {
            Polygon3d poly = new Polygon3d()
            {
                new Point3d(p.X,            p.Y,            p.Z           ),
                new Point3d(p.X + vector.X, p.Y,            p.Z           ),
                new Point3d(p.X + vector.X, p.Y + vector.Y, p.Z + vector.Z),
                new Point3d(p.X,            p.Y + vector.Y, p.Z + vector.Z)
            };

            return new Shape(poly, null, null);
        }


        private void ExportMesh(Mesh mesh)
        {
            MeshExport.ExportToMshFormatv2(base.GetFilePathInOutputFolder("Mesh", "msh"), new List<Mesh>() { mesh });
        }



    }
}