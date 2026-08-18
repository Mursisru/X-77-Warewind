using System.Collections.Generic;
using UnityEngine;

namespace Warewind
{
    internal enum WarewindPhase
    {
        Drop,
        Align,
        Loft,
        Cruise,
        Dive
    }

    /// <summary>Live round state. Fire-and-forget — no live aircraft power/radar.</summary>
    internal sealed class WarewindFlight : MonoBehaviour
    {
        internal WarewindPhase Phase;
        internal PersistentID TargetId;
        internal Vector3 LastKnownPos;
        internal Vector3 LastKnownVel;
        internal Vector3 LaunchPos;
        internal Vector3 LaunchHeading;
        internal float LaunchY;
        internal bool StageSeparated;
        internal bool DockEjected;
        internal int FlaresLeft = WarewindConstants.FlareCount;
        internal float LastFlareTime = -100f;
        internal float Capacitor = WarewindConstants.CapacitorMax;
        internal float LastJamTime = -100f;
        internal float LastSamTime = -100f;
        internal Vector3 SamAim;
        internal bool Armed;
        internal bool FinsOut;
        internal bool ProfileLocked;
        internal float LockRangeM;
        internal float CruiseAltM = WarewindConstants.CruiseAltMaxM;
        internal float LoftEnterAltM = WarewindConstants.CruiseAltMaxM - 2000f;
        internal float LevelStartAltM = WarewindConstants.CruiseAltMaxM - 5000f;
        internal float DiveCommitDistM = WarewindConstants.DiveCommitDistMaxM;
        internal bool ShallowLoft;
        internal bool DirectAttack;
        internal Transform? Engine1;
        internal Transform? Engine2;
        internal Transform? EwDummy;
        internal Vector3 DesiredDir = Vector3.forward;
        internal Vector3 CruiseHeading;
        internal bool CruiseHeadingSet;
        internal float PitchCmd = WarewindConstants.DropPitchDeg;
        internal bool OverTopActive;
        internal float BoosterPunchStartT = -1f;
        internal float PitchScale = 1f;
        internal float LoftPitchMaxEff = WarewindConstants.LoftPitchMaxDeg;
        internal float LoftPitchShallowEff = WarewindConstants.LoftPitchShallowDeg;
        internal float OverTopPitchEff = WarewindConstants.OverTopPitchDeg;
        internal float DiveAngleMinEff = WarewindConstants.DiveAngleMinDeg;
        internal float DiveAngleMaxEff = WarewindConstants.DiveAngleMaxDeg;
        internal readonly List<Transform> FlareSockets = new List<Transform>(4);

        internal void ClearFlares() => FlareSockets.Clear();

        internal void CaptureTarget(Unit? target, Vector3 aimOrFallback, bool preferAim = false)
        {
            if (target != null && !target.disabled)
            {
                TargetId = target.persistentID;
                if (preferAim)
                {
                    LastKnownPos = aimOrFallback;
                    LastKnownVel = Vector3.zero;
                }
                else
                {
                    LastKnownPos = target.transform.position;
                    LastKnownVel = target.rb != null ? target.rb.velocity : Vector3.zero;
                }
            }
            else
            {
                TargetId = PersistentID.None;
                LastKnownPos = aimOrFallback;
                LastKnownVel = Vector3.zero;
            }
        }

        internal void SyncTarget()
        {
            if (!TargetId.IsValid)
                return;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(TargetId), out Unit u) || u == null || u.disabled)
                return;
            LastKnownPos = u.transform.position;
            if (u.rb != null)
                LastKnownVel = u.rb.velocity;
        }
    }
}
