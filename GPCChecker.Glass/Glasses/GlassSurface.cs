using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using GPC.Geometry;
using GPC.Model.Loads;
using GPC.Checker.Glasses.Loads;
using GPC.Checker.Glasses.Models;
using GPC.Model.Restrains;

namespace GPC.Checker.Glasses.Glasses
{
    /// <summary>
    /// Overwrite of Model.Elements.Glasses.GlassSurface in order to add prototype and loads
    /// </summary>
    public sealed class GlassSurface : Model.Elements.Glasses.GlassSurface, IEquatable<GlassSurface>
    {
        private static int _maxId;

        private Prototype _prototype;

        private List<GeometryRestrain> _restrains;

        private List<Load> _loads;

        private List<IParametricLoad> _parametricLoads;



        public Prototype Prototype => _prototype;


        public GlassSurface(Prototype prototype, Shape shape) 
            : base(shape, Guid.NewGuid())
        {
            this._prototype = prototype;
            this._loads = new List<Load>();
            this._parametricLoads = new List<IParametricLoad>();
            this._restrains = new List<GeometryRestrain>();

            Id = _maxId++;
        }


        public GlassSurface(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotImplementedException();
        }


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            throw new NotSupportedException();
        }


        #region Setters

        public void AddLoad(Load load)
        {
            if (load is IParametricLoad)
                throw new ArgumentException();

            _loads.Add(load);
        }


        public void AddParametricLoad(IParametricLoad load)
        {
            _parametricLoads.Add(load);
        } 

        /// <summary>
        /// Add a specific restrain for this surface
        /// </summary>
        /// <param name="geometryRestrain"></param>
        public void AddRestrain(GeometryRestrain geometryRestrain)
        {
            _restrains.Add(geometryRestrain);
        }

        /// <summary>
        /// Add specific restrains for this surface
        /// </summary>
        /// <param name="geometryRestrains"></param>
        public void AddRestrains(List<GeometryRestrain> geometryRestrains)
        {
            _restrains.AddRange(geometryRestrains);
        }

        #endregion

        #region Getter

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The loads of the specific surface. Parametric loads will be converted in specific loads for this surface</returns>
        public List<Load> GetLoads()
        {
            // TODO: implementare conversione carichi parametrici
            return _loads;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The restrains of the specific sursface. Parametric restrain will be converted in the specific restrain for this surface</returns>
        public List<GeometryRestrain> GetRestrains()
        {
            // TODO: implementare conversione restrain parametrici
            return _restrains;
        } 

        #endregion

        #region Equals - HashCode

        public override bool Equals(object obj)
        {
            return Equals(obj as GlassSurface);
        }

        public bool Equals(GlassSurface other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return !(other is null) && other._prototype.Equals(_prototype) && other._loads.Equals(_loads) && base.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = 23;
            hashCode = hashCode * -17 + base.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<Prototype>.Default.GetHashCode(_prototype);
            hashCode = hashCode * -17 + EqualityComparer<List<Load>>.Default.GetHashCode(_loads);
            return hashCode;
        }

        #endregion
    }
}
