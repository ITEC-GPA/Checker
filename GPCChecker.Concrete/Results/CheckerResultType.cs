using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.SectionSolver;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
	public abstract class CheckerResultType : Model.ModelObjectId
	{
		protected IConcreteSection _section;
		protected readonly Standard _standard;

		public IConcreteSection ConcreteSection => _section;

		public Standard Standard => _standard;


		#region Public Constructor

		public CheckerResultType(IConcreteSection section, Standard standard, int id = IDUNASSIGNED)
			:base(id)
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

		#region Public override methods

		public override bool Equals(object obj)
		{
			return obj is CheckerResultType type &&
				   base.Equals(obj) &&
				   EqualityComparer<IConcreteSection>.Default.Equals(_section, type._section);
		}

		public override int GetHashCode()
		{
			int hashCode = -23;
			hashCode = hashCode * -17 + base.GetHashCode();
			hashCode = hashCode * -17 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_section);
			return hashCode;
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