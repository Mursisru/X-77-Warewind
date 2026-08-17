using Warewind.Runtime;
using UnityEngine;

namespace Warewind
{
    /// <summary>Bind FBX dummy arrows to motor FX / flare sockets / EW look transform.</summary>
    internal static class WarewindFxBind
    {
        internal static void Bind(Missile missile, WarewindFlight flight)
        {
            if (missile == null || flight == null)
                return;
            Transform? vis = WarewindVisualStamp.FindVisual(missile.transform);
            if (vis == null)
                return;

            flight.Engine1 = TransformBinder.FindByAliases(vis, WarewindConstants.Engine1Aliases);
            flight.Engine2 = TransformBinder.FindByAliases(vis, WarewindConstants.Engine2Aliases);
            Transform? ew = TransformBinder.FindExactByAliases(vis, WarewindConstants.EwAliases);
            flight.EwDummy = WarewindEw.IsRotatableAntenna(ew) ? ew : null;
            WarewindEw.EnsureAntenna(missile, flight);

            flight.ClearFlares();
            TransformBinder.CollectByAliases(vis, WarewindConstants.FlareAliases, flight.FlareSockets);
            if (flight.FlareSockets.Count == 0 && flight.Engine2 != null)
                flight.FlareSockets.Add(flight.Engine2);

            // Flame must live on WarewindVisual sockets — stock AAM2 FX are under hidden mesh.
            WarewindMotorFx.Bind(missile, flight);
        }
    }
}
