using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public abstract class CheckerResultType : ModelObjectId, ISerializable
    {
        #region Variables

        protected readonly IConcreteSection _section;
        protected readonly Standard _standard;

        #endregion

        #region Properties

        public IConcreteSection ConcreteSection => _section;

        public Standard Standard => _standard;

		#endregion

		#region Constructor

		public CheckerResultType(IConcreteSection section, Standard standard, int id = IDUNASSIGNED)
            : base(id)
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
        }

        protected CheckerResultType(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _section = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
        }

        #endregion

        #region Equals, hashcode, operators

        public override bool Equals(object obj)
        {
            return obj is CheckerResultType type &&
                   base.Equals(obj) &&
                   EqualityComparer<IConcreteSection>.Default.Equals(_section, type._section);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = -23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + _section.GetHashCode();
                hashCode = hashCode * -17 + _standard.GetHashCode();
                return hashCode;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ConcreteSection", _section);
            info.AddValue("Standard", _standard);
        }

		#endregion
	}
}