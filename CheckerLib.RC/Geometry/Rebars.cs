using CheckerLib.RC.Materials;
using System.Collections;
using System.Collections.Generic;
using Utilities.Geometry;

namespace CheckerLib.RC.Geometry
{
    public class Rebars : IEnumerable<Rebar>
    {
        private readonly List<Rebar> _bars;

        internal Rebars()
        {
            _bars = new List<Rebar>();
        }

        #region IEnumerable

        public IEnumerator<Rebar> GetEnumerator()
        {
            return _bars.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _bars.GetEnumerator();
        }

        #endregion IEnumerable

        public int Count => _bars.Count;

        public Rebar this[int index] => _bars[index];

        internal void AddRebar(double diameter, double effectiveArea, Point2d position, RebarSteel rebarSteel)
        {
            _bars.Add(new Rebar(diameter, effectiveArea, position, rebarSteel));
        }
        internal void AddRebar(Rebar rebar)
        {
            _bars.Add(rebar);
        }
    }
}