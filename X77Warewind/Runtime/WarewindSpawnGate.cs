using System.Reflection;
using Warewind.Blueprinter;
using Warewind.Bootstrap;
using Warewind.Runtime;
using UnityEngine;

namespace Warewind
{
    internal sealed class WarewindTag : MonoBehaviour
    {
    }

    /// <summary>Own pending token. Never shares Mk54SpawnGate.InFlight.</summary>
    internal static class WarewindSpawnGate
    {
        private static readonly FieldInfo? InfoField =
            typeof(Missile).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic);

        private const float PendingTtlS = 8f;
        internal static int Pending;
        internal static bool InFlight;
        private static float _until = -1f;
        private static Unit? _pendingTarget;
        private static Vector3 _pendingAimLocal;
        private static bool _hasPendingAim;

        internal static void NoteFire(MountedMissile? mount, Unit? target, GlobalPosition aimpoint)
        {
            Expire();
            Pending++;
            _until = Time.realtimeSinceStartup + PendingTtlS;
            _pendingTarget = target;
            _pendingAimLocal = aimpoint.ToLocalPosition();
            _hasPendingAim = true;
            SyncSharedInfo(mount);
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind NoteFire pending={Pending} target={(target != null ? target.name : "aim")} aim={_pendingAimLocal}");
        }

        /// <summary>Recent Warewind Fire even if Pending token was stolen by unrelated SpawnMissile.</summary>
        internal static bool HasRecentFire() =>
            _until > 0f && Time.realtimeSinceStartup <= _until;

        /// <summary>Shared AAM2 shell spawn that missed TryBegin — reclaim as Warewind.</summary>
        internal static bool ShouldRescueClaim(GameObject? prefab)
        {
            if (!HasRecentFire())
                return false;
            GameObject? fly = WarewindBootstrap.Definition?.unitPrefab;
            return fly != null && ReferenceEquals(prefab, fly);
        }

        internal static void SyncSharedInfo(MountedMissile? mount)
        {
            WeaponInfo? shared = WarewindBootstrap.Info;
            GameObject? fly = WarewindBootstrap.Definition?.unitPrefab;
            if (shared == null)
                return;
            if (fly != null)
                shared.weaponPrefab = fly;
            if (mount != null)
                mount.info = shared;
        }

        internal static bool TryBegin()
        {
            Expire();
            if (Pending <= 0)
                return false;
            Pending--;
            InFlight = true;
            return true;
        }

        internal static void End() => InFlight = false;

        private static void Expire()
        {
            if (Pending <= 0)
                return;
            if (_until < 0f || Time.realtimeSinceStartup <= _until)
                return;
            WarewindPlugin.ModLog?.LogWarning($"Warewind pending expired ({Pending})");
            Pending = 0;
            _until = -1f;
            ClearPendingTarget();
        }

        private static void ClearPendingTarget()
        {
            _pendingTarget = null;
            _hasPendingAim = false;
        }

        internal static void Claim(Missile missile, Unit? fireTarget)
        {
            if (missile == null)
                return;

            if (WarewindBootstrap.Definition != null)
                missile.definition = WarewindBootstrap.Definition;
            if (WarewindBootstrap.Info != null)
                InfoField?.SetValue(missile, WarewindBootstrap.Info);

            if (missile.GetComponent<WarewindTag>() == null)
                missile.gameObject.AddComponent<WarewindTag>();
            missile.NetworkunitName = WarewindConstants.UnitName;
            missile.SetThrottle(0f);
            WarewindMotors.Apply(missile);
            WarewindShellPrep.Apply(missile);
            WarewindSurvivability.Apply(missile);
            WarewindAero.Apply(missile);

            WarewindFlight? existing = missile.GetComponent<WarewindFlight>();
            if (existing != null)
            {
                // Refresh lock if Fire left a better aim/target after first Claim.
                ApplyPendingLock(existing, missile, fireTarget);
                if (WarewindVisualStamp.FindVisual(missile.transform) == null && NobpContent.WarewindVisual != null)
                    WarewindVisualStamp.Stamp(missile.gameObject, NobpContent.WarewindVisual, live: true);
                WarewindFxBind.Bind(missile, existing);
                WarewindDockEject.TryEject(missile, existing);
                return;
            }

            Rigidbody? rb = missile.rb != null ? missile.rb : missile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                Vector3 src = missile.startingVelocity.sqrMagnitude > 0.01f ? missile.startingVelocity : rb.velocity;
                if (src.sqrMagnitude > 0.01f)
                    rb.velocity = src;
                rb.angularVelocity = Vector3.zero;
            }

            AttachFlight(missile, fireTarget);

            NobpContent.TryLoad();
            if (WarewindVisualStamp.FindVisual(missile.transform) == null && NobpContent.WarewindVisual != null)
                WarewindVisualStamp.Stamp(missile.gameObject, NobpContent.WarewindVisual, live: true);

            WarewindFlight? f = missile.GetComponent<WarewindFlight>();
            if (f != null)
            {
                WarewindFxBind.Bind(missile, f);
                WarewindDockEject.TryEject(missile, f);
            }

            NetworkPrefabPrep.LogState("Warewind Claim", missile.gameObject);
        }

        internal static void Ensure(Missile missile)
        {
            if (missile == null || !WarewindBootstrap.IsOurs(missile))
                return;
            if (missile.GetComponent<WarewindFlight>() == null)
                Claim(missile, _pendingTarget);
            else
                ApplyPendingLock(missile.GetComponent<WarewindFlight>()!, missile, _pendingTarget);
        }

        private static void AttachFlight(Missile missile, Unit? fireTarget)
        {
            WarewindFlight f = missile.GetComponent<WarewindFlight>();
            if (f == null)
                f = missile.gameObject.AddComponent<WarewindFlight>();

            Vector3 pos = missile.transform.position;
            f.LaunchPos = pos;
            f.LaunchY = pos.y;
            f.LaunchHeading = Horiz(missile.startingVelocity);
            if (f.LaunchHeading.sqrMagnitude < 0.01f && missile.rb != null)
                f.LaunchHeading = Horiz(missile.rb.velocity);
            if (f.LaunchHeading.sqrMagnitude < 0.01f)
                f.LaunchHeading = Horiz(missile.transform.forward);
            if (f.LaunchHeading.sqrMagnitude < 0.01f)
                f.LaunchHeading = Vector3.forward;

            ApplyPendingLock(f, missile, fireTarget);
            f.Phase = WarewindPhase.Drop;
        }

        private static void ApplyPendingLock(WarewindFlight f, Missile missile, Unit? fireTarget)
        {
            Unit? t = fireTarget != null ? fireTarget : _pendingTarget;
            if (t == null && missile.targetID.IsValid)
                UnitRegistry.TryGetUnit(new PersistentID?(missile.targetID), out t);

            Vector3 pos = missile.transform.position;
            Vector3 fallback = pos + f.LaunchHeading * 20000f;
            if (f.LaunchHeading.sqrMagnitude < 0.01f)
                fallback = pos + Horiz(missile.transform.forward) * 20000f;

            bool useAim = _hasPendingAim;
            Vector3 aim = useAim ? _pendingAimLocal : fallback;
            if (useAim)
            {
                Vector3 d = aim - pos;
                d.y = 0f;
                if (d.sqrMagnitude < 250000f)
                    useAim = false;
            }

            if (useAim)
            {
                f.CaptureTarget(t, aim, preferAim: true);
                f.SamAim = aim;
            }
            else if (t != null && !t.disabled)
            {
                f.CaptureTarget(t, t.transform.position);
                f.SamAim = t.transform.position;
            }
            else
            {
                f.CaptureTarget(null, fallback);
                f.SamAim = fallback;
            }

            if (t != null && !t.disabled)
                missile.SetTarget(t);

            WarewindProfile.Lock(f, pos, f.LastKnownPos);
            ClearPendingTarget();
            float dx = f.LastKnownPos.x - pos.x;
            float dz = f.LastKnownPos.z - pos.z;
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind lock tgt={(t != null ? t.name : "none")} distH={Mathf.Sqrt(dx * dx + dz * dz):F0}m");
        }

        internal static bool IsOurFlyPrefab(GameObject? go)
        {
            if (go == null)
                return false;
            GameObject? fly = WarewindBootstrap.Definition?.unitPrefab;
            if (fly != null && ReferenceEquals(go, fly))
                return true;
            if (go.GetComponent<WarewindTag>() != null || go.GetComponentInChildren<WarewindTag>(true) != null)
                return true;
            return go.name == WarewindConstants.FlyPrefabName;
        }

        private static Vector3 Horiz(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.zero;
        }
    }
}
