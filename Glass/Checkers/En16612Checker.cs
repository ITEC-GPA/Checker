using GPC.Checkers.GlassV2.Attributes;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Checkers
{
	public class En16612Checker : GlassChecker
	{
		public En16612Checker(GlassCheckerAttribute checkerAttribute, Standard standard, En16612CheckerOptions options, int id = IDUNASSIGNED)
			: base(checkerAttribute, standard, options, id)
		{

		}

		protected En16612Checker(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{

		}



		#region Nested class

		[Serializable]
		public class En16612CheckerOptions : GlassCheckerOptions, ISerializable
		{
			public En16612CheckerOptions()
			{

			}

			protected En16612CheckerOptions(SerializationInfo info, StreamingContext context)
			{

			}

			public override bool Equals(object obj)
			{
				return obj is En16612CheckerOptions options;
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

			public override void GetObjectData(SerializationInfo info, StreamingContext context)
			{
			}

			public static bool operator ==(En16612CheckerOptions left, En16612CheckerOptions right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(En16612CheckerOptions left, En16612CheckerOptions right)
			{
				return !(left == right);
			}
		}

		#endregion
	}
}
