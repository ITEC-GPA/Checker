using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPCChecker.Steel.PanelsStability
{
    public enum Code { Eurocode, ECP205_2001_ASD };
    public enum StiffenerLongitudinalType { Open, Closed, Flat, Warping };
    public enum WebStiffedType { Stiffened, Unstiffened };
    public enum StiffenerTransversalType { Rigid, NonRigid, SingleSided, DoubleSided };
    public enum PlateSupportType { Internal, Outstand };
}
