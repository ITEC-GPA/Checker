using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using GPC.Geometry;
using GPC.Checkers.Glasses.Loads;
using GPC.Checkers.Glasses.Models;
using GPC.Model.Restrains;
using GPC.Geometry.Meshes;
using GPC.Utilities.Extensions;
using GPC.Model.Loads;

namespace GPC.Checkers.Glasses.Glasses
{
    /// <summary>
    /// Overwrite of Model.Elements.Glasses.GlassSurface in order to add prototype and loads
    /// </summary>
    public sealed class GlassSurface : Model.Elements.Glasses.GlassSurface, IEquatable<GlassSurface>
    {
        public enum LoadRestrainCondition
        {
            AsSurface, 
            FourSidesClimate
        }


        private static int _maxId;

        private readonly Prototype _prototype;
        private readonly List<GeometryRestrain> _restrains;
        private readonly List<IGlassLoad> _loads;
        private readonly List<IParametricLoad> _parametricLoads;
        private readonly Mesh.GenerateOptions _meshOptions;
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

        public GlassSurface(Prototype prototype, Shape shape) 
            : base(shape, _maxId++, Guid.NewGuid())
        {
            _prototype = prototype;
            _loads = new List<IGlassLoad>();
            _parametricLoads = new List<IParametricLoad>();
            _restrains = new List<GeometryRestrain>();
            _meshOptions = (Mesh.GenerateOptions)prototype.MeshOptions.Clone();

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


        public bool IsPlanar()
        {
            return true; // TODO: modificare una volta aggiunto il supporto alle goemetrie 3d
        }


        /// <returns><see langword="True"/> if glass is rectangular. <see langword="False"/> if otherwise or not planar.</returns>
        public bool IsRectangular()
        {
            if (!IsPlanar())
                return false;

            var fill = (Polygon3d)Shape.Fill.Clone();
            fill.RemoveAlignedPoints();
            fill.RemoveDuplicatedPoints();

            var border = fill.Explode();

            if (border.Count != 4)
                return false;

            for (int i = 0; i < border.Count - 1; i++)
            {
                var v1 = border[i].ToVector();
                v1.Unitize();
                var v2 = border[i + 1].ToVector();
                v2.Unitize();

                if (Math.Abs(v1.DotProduct(v2)) > GeometryBase.GetDefaultAngularTolerance())
                    return false;
            }

            return true;
        }


        /// <summary>
        /// Add a load to the surface
        /// </summary>
        public void AddLoad(IGlassLoad load)
        {
            if (!(load is IGlassLoad))
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
        /// Remove a specific restrain to this surface
        /// </summary>
        public bool RemoveRestrain(GeometryRestrain geometryRestrain)
        {
            return _restrains.Remove(geometryRestrain);
        }

        /// <summary>
        /// Remove a specific restrain to this surface
        /// </summary>
        public void RemoveRestrainAt(int index)
        {
            _restrains.RemoveAt(index);
        }


        /// <returns>The loads of the specific surface. Parametric loads will be converted in specific loads for this surface</returns>
        public List<IGlassLoad> GetLoads()
        {
            // TODO: implementare conversione carichi parametrici
            return _loads;
        }


        /// <returns>The restrains of the specific sursface. Parametric restrain will be converted in the specific restrain for this surface</returns>
        public List<GeometryRestrain> GetRestrains()
        {
            // TODO: implementare conversione restrain parametrici
            return _restrains.ToList(); // shallow copy
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
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<Prototype>.Default.GetHashCode(_prototype);

                foreach (var el in _loads)
                {
                    hashCode += 17 * el.GetHashCode();
                }

                foreach (var el in _restrains)
                {
                    hashCode += 17 * EqualityComparer<GeometryRestrain>.Default.GetHashCode(el);
                }

                foreach (var el in _parametricLoads)
                {
                    hashCode += +17 * EqualityComparer<IParametricLoad>.Default.GetHashCode(el);
                }

                return hashCode; 
            }
        }

        public static bool operator ==(GlassSurface obj1, GlassSurface obj2)
        {
            if (obj1 is null)
            {
                return obj2 is null;
            }

            if (ReferenceEquals(obj1, obj2))
                return true;

            return obj1.Equals(obj2);
        }

        public static bool operator !=(GlassSurface obj1, GlassSurface obj2)
        {
            return !(obj1 == obj2);
        }

        #endregion
    }
}
