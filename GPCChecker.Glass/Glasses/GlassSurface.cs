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
using GPC.Geometry.Meshes;
using GPC.Utilities.Extensions;

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
        private Mesh.GenerateOptions _meshOptions;
        private Checkers.Checker _checker;

        #region Properties

        public Prototype Prototype => _prototype;

        public Mesh.GenerateOptions MeshOptions => _meshOptions;

        public Checkers.Checker Checker
        {
            get => _checker;
            set => _checker = value;
        }

        #endregion

        #region Constructors

        public GlassSurface(Prototype prototype, Shape shape, Mesh.GenerateOptions options) 
            : base(shape, Guid.NewGuid())
        {
            _prototype = prototype;
            _loads = new List<Load>();
            _parametricLoads = new List<IParametricLoad>();
            _restrains = new List<GeometryRestrain>();
            _meshOptions = options;

            Id = _maxId++;
        }


        public GlassSurface(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Public functions

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            throw new NotSupportedException();
        }

        /// <summary>
        /// Add a load to the surface
        /// </summary>
        public void AddLoad(Load load)
        {
            if (load is IParametricLoad)
                throw new ArgumentException();

            _loads.Add(load);
        }


        /// <summary>
        /// Add a parametric load to the surface
        /// </summary>
        public void AddParametricLoad(IParametricLoad load)
        {
            _parametricLoads.Add(load);
        } 


        /// <summary>
        /// Add a specific restrain to this surface
        /// </summary>
        public void AddRestrain(GeometryRestrain geometryRestrain)
        {
            _restrains.Add(geometryRestrain);
        }


        /// <inheritdoc cref="AddRestrain(GeometryRestrain)"/>
        public void AddRestrains(List<GeometryRestrain> geometryRestrains)
        {
            _restrains.AddRange(geometryRestrains);
        }

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

        #region Equals - HashCode - Operators

        public override bool Equals(object obj)
        {
            return Equals(obj as GlassSurface);
        }

        public bool Equals(GlassSurface other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return !(other is null) && other._prototype.Equals(_prototype) 
                                    && other._loads.ScrambledEquals(_loads)
                                    && other._restrains.ScrambledEquals(_restrains)
                                    && other._parametricLoads.ScrambledEquals(_parametricLoads)
                                    && base.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = 23;
            hashCode = hashCode * -17 + base.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<Prototype>.Default.GetHashCode(_prototype);

            foreach (var el in _loads)
            {
                hashCode = hashCode + 17 * EqualityComparer<Load>.Default.GetHashCode(el);
            }

            foreach (var el in _restrains)
            {
                hashCode = hashCode + 17 * EqualityComparer<GeometryRestrain>.Default.GetHashCode(el);
            }

            foreach (var el in _parametricLoads)
            {
                hashCode = hashCode + 17 * EqualityComparer<IParametricLoad>.Default.GetHashCode(el);
            }

            return hashCode;
        }

        public static bool operator ==(GlassSurface obj1, GlassSurface obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;

            if (obj1 is null || obj2 is null)
                return false;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(GlassSurface obj1, GlassSurface obj2)
        {
            return !(obj1 == obj2);
        }

        #endregion
    }
}
