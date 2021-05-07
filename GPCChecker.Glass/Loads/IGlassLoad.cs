using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Wrappers;

namespace GPC.Checkers.Glasses.Loads
{
    public interface IGlassLoad
    {

        GlassPanelWrapper.GlassPanelPositions GlassPanelPosition { get; }

    }
}
