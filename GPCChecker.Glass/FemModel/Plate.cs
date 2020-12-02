using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GPC.Model.Elements;
using GPC.Model.Elements.Glasses;

namespace GPC.Checker.Glasses.FemModel
{
    internal class Plate : GPC.Model.FEM.Plate
    {
        private int _globalId;


        public int GlobalId { get { return _globalId; } set { _globalId = value; } }



        public Plate(IGlassPanelProperty property, int index, Node node1, Node node2, Node node3)
            : base(Guid.NewGuid(), (ElementProperty)property, index, new Node[] { node1, node2, node3 })
        {

        }


        public Plate(IGlassPanelProperty property, int index, Node node1, Node node2, Node node3, Node node4)
            : base(Guid.NewGuid(), (ElementProperty)property, index, new Node[] { node1, node2, node3, node4 })
        {

        }

        protected Plate(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {

        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
