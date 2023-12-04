using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Glass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Attributes
{
    [Serializable]
    public class GlassCheckerAttribute : ModelObjectId, ISerializable
    {
        #region Variables

        protected readonly GlassPlateProperty _section;
        protected readonly List<ResultBeamForces> _forces;

        #endregion

        #region Properties

        public GlassPlateProperty Section => _section;

        public List<ResultBeamForces> Forces => _forces;

        #endregion

        #region Constructor

        public GlassCheckerAttribute(GlassPlateProperty section, IEnumerable<ResultBeamForces> forces, int id = ModelObjectId.IDUNASSIGNED)
            : base(id)
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _forces = forces.Count() == 0 ? new List<ResultBeamForces>() : forces.ToList();
        }

        public GlassCheckerAttribute(GlassPlateProperty section, int id = ModelObjectId.IDUNASSIGNED)
            : this(section, null, id)
        {
        }

        protected GlassCheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _section = (GlassPlateProperty)info.GetValue("Sections", typeof(GlassPlateProperty));
            _forces = (List<ResultBeamForces>)info.GetValue("Forces", typeof(List<ResultBeamForces>));
        }

        #endregion

        #region Equals - hashcode - operators - serialization

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Sections", _section, typeof(IConcreteSection));
            info.AddValue("Forces", _forces, typeof(List<ResultBeamForces>));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is GlassCheckerAttribute objCasted) &&
                _section.Equals(objCasted.Section) &&
                _forces.SequenceEqual(objCasted.Forces) &&
                base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _section.GetHashCode();
                for (int i = 0; i < _forces.Count; i++)
                    hashCode = hashCode * -17 + _forces[i].GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}
