//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using Utilities;
//using GPC.Geometry;

//namespace Checker.ReinforcedConcrete
//{
//    public class RCBars : IEnumerable<RCBar>
//    {
//        private readonly List<RCBar> _bars;

//        internal RCBars()
//        {
//            _bars = new List<RCBar>();
//        }

//        #region IEnumerable

//        public IEnumerator<RCBar> GetEnumerator()
//        {
//            return _bars.GetEnumerator();
//        }

//        IEnumerator IEnumerable.GetEnumerator()
//        {
//            return _bars.GetEnumerator();
//        }

//        #endregion

//        public int Count => _bars.Count;

//        public RCBar this[int index] => _bars[index];

//        internal void AddBar(double diameter,
//                             double effectiveArea,
//                             Point2d position,
//                             double epsilon0,
//                             double elasticModulusE,
//                             double fyk)
//        {
//            _bars.Add(new RCBar(diameter, effectiveArea, position, epsilon0, elasticModulusE, fyk));
//        }

//    }

//    public class RCBar
//    {
//        public readonly double Diameter;
//        public readonly double EffectiveArea;
//        public readonly Point2d Position;
//        public readonly double Epsilon0;
//        public readonly double ElasticModulusE;
//        public readonly double Fyk;

//        internal RCBar(double diameter,
//                       double effectiveArea,
//                       Point2d position,
//                       double epsilon0,
//                       double elasticModulusE,
//                       double fyk)
//        {
//            Diameter = diameter;
//            EffectiveArea = effectiveArea;
//            Position = new Point2d(position);
//            Epsilon0 = epsilon0;
//            ElasticModulusE = elasticModulusE;
//            Fyk = fyk;
//        }
//    }
//}
