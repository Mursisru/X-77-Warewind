using Warewind.Runtime;
using UnityEngine;

namespace Warewind
{
    /// <summary>Unparent stage-1 mesh like VLSBooster.Burnout. Never sets boosterIsAttached.</summary>
    internal static class WarewindStageSep
    {
        internal static void TrySeparate(Missile missile, WarewindFlight flight)
        {
            if (missile == null || flight == null || flight.StageSeparated)
                return;
            if (WarewindMotors.MotorStage(missile) < 1 && WarewindMotors.Stage0Fuel(missile) > 0.05f)
                return;

            flight.StageSeparated = true;
            Transform? vis = WarewindVisualStamp.FindVisual(missile.transform);
            Transform? stage1 = vis != null
                ? TransformBinder.FindByAliases(vis, WarewindConstants.Stage1Aliases)
                : null;
            if (stage1 == null)
            {
                WarewindPlugin.ModLog?.LogWarning("Warewind stage1 mesh not found — motors advanced without visual sep.");
                WarewindMotors.SubtractDryMass(missile, WarewindConstants.Stage1DryMassKg);
                return;
            }

            Vector3 vel = missile.rb != null ? missile.rb.velocity : Vector3.zero;
            Vector3 pos = stage1.position;
            Quaternion rot = stage1.rotation;
            stage1.SetParent(null, true);
            stage1.position = pos;
            stage1.rotation = rot;

            Rigidbody rb = stage1.GetComponent<Rigidbody>();
            if (rb == null)
                rb = stage1.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(40f, WarewindConstants.Stage1DryMassKg);
            rb.drag = 0.12f;
            rb.angularDrag = 0.02f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.velocity = vel;
            rb.angularVelocity = Vector3.zero;
            Object.Destroy(stage1.gameObject, WarewindConstants.Stage1DestroyS);
            WarewindMotors.SubtractDryMass(missile, WarewindConstants.Stage1DryMassKg);
            WarewindMotorFx.StopStage(missile, 0);
            WarewindPlugin.ModLog?.LogInfo("Warewind stage1 separated.");
        }
    }
}
