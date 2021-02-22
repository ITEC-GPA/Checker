using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using GPC.Geometry;
using GPC.Model.Loads;
using GPC.Checker.Glasses.Models;


namespace GPC.Checker.Glasses.Glasses
{
    public sealed class GlassSurface : Model.Elements.Glasses.GlassSurface, IEquatable<GlassSurface>
    {

        private Prototype _prototype;

        private List<Load> _loads;


        public Prototype Prototype => _prototype;
        public List<Load> Load => _loads;


        public GlassSurface(Prototype prototype, Shape shape) 
            : base(shape, Guid.NewGuid())
        {
            this._prototype = prototype;
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

        public override bool Equals(object obj)
        {
            return Equals(obj as GlassSurface);
        }


        public bool Equals(GlassSurface other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return !(other is null) && other._prototype.Equals(_prototype)
                                    && other._loads.Equals(_loads) 
                                    && base.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = 23;
            hashCode = hashCode * -17 + base.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<Prototype>.Default.GetHashCode(_prototype);
            hashCode = hashCode * -17 + EqualityComparer<List<Load>>.Default.GetHashCode(_loads);
            return hashCode;
        }
    }
}
