using GPC.Checkers.GlassV2.Attributes;
using GPC.Checkers.GlassV2.Solvers;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Checkers
{
	[Serializable]
	public abstract class GlassChecker : Checker, ISerializable
	{
		protected readonly GlassCheckerAttribute _checkerAttributes;
		protected readonly Solver _solver;

		public GlassCheckerOptions SectionCheckerOptions => (GlassCheckerOptions)_options;




		public GlassChecker(GlassCheckerAttribute checkerAttribute, Standard standard, GlassCheckerOptions options, int id)
			: base(standard, options, id)
		{
			_checkerAttributes = checkerAttribute;
		}

		protected GlassChecker(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_solver = (Solver)info.GetValue("Solver", typeof(Solver));

		}


		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Solver", _solver);
		}

		#region Nested class

		[Serializable]
		public class GlassCheckerOptions : Options, ISerializable
		{
			public GlassCheckerOptions()
			{

			}

			protected GlassCheckerOptions(SerializationInfo info, StreamingContext context)
			{

			}

			public override bool Equals(object obj)
			{
				return obj is GlassCheckerOptions options;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hashCode = -17;
					hashCode = hashCode * base.GetHashCode();
					return hashCode;
				}
			}

			public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
			{
			}

			public static bool operator ==(GlassCheckerOptions left, GlassCheckerOptions right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(GlassCheckerOptions left, GlassCheckerOptions right)
			{
				return !(left == right);
			}
		}

		#endregion
	}
}
