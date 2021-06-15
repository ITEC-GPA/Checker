using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Glasses.Wrappers;
using GPC.Checkers.Glasses.Glasses;

namespace GPC.Checkers.Glasses.Loads
{
    public interface IGlassLoad
    {

        GlassPanelWrapper.GlassPanelPositions GlassPanelPosition { get; }

        GlassSurface.LoadRestrainCondition LoadRestrainCondition { get; }

    }
}
