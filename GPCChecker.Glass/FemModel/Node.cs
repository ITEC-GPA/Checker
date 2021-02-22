using System;
using System.Diagnostics;
using System.Linq;
using GPC.Geometry;
using GPC.Model.Elements;

namespace GPC.Checker.Glasses.FemModel
{
    //[DebuggerDisplay("{DebuggerDisplay()}")]
    //internal class Node : GPC.Model.FEM.Node
    //{
    //    private int _globalId;

    //    public int GlobalId { get { return _globalId; } set { _globalId = value; } }


    //    public Node(Point3d position, int nodeIndex, Restrain restrain)
    //        : base(Guid.NewGuid(), position, nodeIndex, restrain)
    //    {

    //    }

    //    public Node(Guid guid, Point3d position, int nodeIndex, Restrain restrain) 
    //        : base(guid, position, nodeIndex, restrain)
    //    {

    //    }

    //    public override string ToString()
    //    {
    //        return base.ToString() + " GID: " + GlobalId;
    //    }

    //    private string DebuggerDisplay()
    //    {
    //        return $"Index:{NodeIndex} X:{_position.X} Y:{_position.Y} Z:{_position.Z}";
    //    }
    //}
}
