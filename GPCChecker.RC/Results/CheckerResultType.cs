using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	public abstract class CheckerResultType : Model.ModelObjectId
	{
		protected IConcreteSection _section;

		public IConcreteSection ConcreteSection => _section;

		#region Public Constructor

		public CheckerResultType(IConcreteSection section, int id = IDUNASSIGNED)
			:base(id)
		{
			_section = section ?? throw new ArgumentNullException(nameof(section));
		}

		protected CheckerResultType(SerializationInfo info, StreamingContext context) 
			: base(info, context)
		{
			_section = (IConcreteSection)info.GetValue("ConcreteSection", typeof(IConcreteSection));
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
		}

		#endregion
	}
}