//using CheckerLib.RC.Materials;
//using Utilities.Geometry;
//using CheckerLib.Common.Geometry;
//using GPC.Model;

//namespace CheckerLib.RC.Geometry
//{
//    public class RCSection : Section
//    {
//        private readonly Rebars _rebars;
//        public RCSection(Shapes shapes) : base(shapes)
//        {
//        }
//        public RCSection(Shapes shapes, Rebars rebars) : this(shapes)
//        {
//            this._rebars = rebars;
//        }
//        public void AddRebar(double diameter, double effectiveArea, Point2d position, RebarSteel rebarSteel)
//        {
//            _rebars.AddRebar(diameter, effectiveArea, position, rebarSteel);
//        }
//        public void AddRebar(Rebar rebar)
//        {
//            _rebars.AddRebar(rebar);
//        }

//        internal Rebars Rebars => _rebars;
//    }
//}
