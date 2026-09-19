
#undef Timed

using CommNet.Network;
using PEKKAUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModulePekkaAero
{
    public class ModulePekkaAero : ModuleLiftingSurface, ILiftProvider, ITorqueProvider
    {
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool debug = false;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string FlapName = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool changeCoM = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool activated = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string CoLName = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string CoDName = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 Actuation = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 LiftShift = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float WingLength = 0;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float WingArea = 0;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float LiftCoeff = 0;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float LiftToDragRatio = 1;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 DragArea = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 PosDragCoefficients = new Vector3(1, 1, 1);
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 NegDragCoefficients = new Vector3(1, 1, 1);
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "P0", guiName = "PitchActuation", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0, maxValue = 1, stepIncrement = 0.01f)]
        public float PitchActuation = 1.0f;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "P0", guiName = "YawActuation", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0, maxValue = 1, stepIncrement = 0.01f)]
        public float YawActuation = 1.0f;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "P0", guiName = "RollActuation", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0, maxValue = 1, stepIncrement = 0.01f)]
        public float RollActuation = 1.0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float FlapSpeed = 1f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float retractedPos1 = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float retractedPos2 = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float CurrentRotation = 0.0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float defaultRot = 0.5f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string DeployAnim = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float DeployTime = 1;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Lift", guiFormat = "F0", guiUnits = "kN", groupName = groupName, groupDisplayName = groupName)]
        public double LiftLen = 0d;
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "AoA", guiFormat = "F1", guiUnits = "°", groupName = groupName, groupDisplayName = groupName)]
        public double AoA = 0d;
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Drag", guiFormat = "N0", guiUnits = "kN", groupName = groupName, groupDisplayName = groupName)]
        public double DragLen = 0d;
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "L/D", guiFormat = "N2", guiUnits = "", groupName = groupName, groupDisplayName = groupName)]
        public double LoD = 0d;
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Actuation", guiFormat = "N0", guiUnits = "", groupName = groupName, groupDisplayName = groupName)]
        public Vector3 actuation = new Vector3(1,1,1);

        public const string groupName = "PekkaAero";
        const string toggleDeploy = "Toggle Flaps";
        const string toggleRetracted = "Toggle Retracted Flap Position";
        const string Deploy = "Deploy Flaps";
        const string Retract = "Retract Flaps";
        const string RetractToggle = "Toggle Retracted Position";

        bool automation = false;
        bool isAnimating = false;
        bool isDeploying = false;

        Transform Flap;
        Transform CoL = null;
        Transform CoD = null;

        Vector3 finalDragCoefficients = Vector3d.zero;

        Curve CenteredCurve = null;
        Curve AoACurve = null;
        Curve CDnxCurve = null;
        Curve CDnyCurve = null;
        Curve CDnzCurve = null;
        Curve CDpxCurve = null;
        Curve CDpyCurve = null;
        Curve CDpzCurve = null;
        Curve CDCurve = null;
        Curve CLCurve = null;
        Curve LDCurve = null;

        //float SpeedErr = 1f;
        //float RotErr = 1f;
        float Speed = 0.0f;

        float retractedPos = 0f;
        float WantedRotation = 0.0f;
        float extraheight = 0.0f;
        double mach = 0.0f;

        Vector3 baseLiftForce = Vector3.zero;
        Vector3 drag = Vector3.zero;
        Vector3 trueDrag = Vector3.zero;
        Vector3 dragLift = Vector3.zero;
        Vector3 lift = Vector3.zero;
        Vector3 trueLift = Vector3.zero;
        Vector3 scaleDrag = new Vector3(1f, 1f, 1f);//new Vector3(0.1f, 0.1f, 0.005f);//new Vector3(3.3f, 3.3f, 3.3f);

        Vector3 diffSaved = new Vector3(1, 1, 1);
        Vector3 ld1Saved = new Vector3(1, 1, 1);
        Vector3 ld2Saved = new Vector3(1, 1, 1);

        Vector3d oldPosL = new Vector3d();
        Vector3d oldPosD = new Vector3d();
        Vector3d rotVel = new Vector3d();

        bool start = true;
        string[] updateRemoveModules = { "FARAeroPartModule", "FARPartModule", "GeometryPartModule" };

        RenderLine totalLine;
        RenderLine dragLine;
        RenderLine liftLine;

        AnimationHelper Animation;

#if Timed
        PerfTimer timer = new PerfTimer();
#endif

        [KSPEvent(guiActive = true, guiName = Deploy, active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void Deploying()
        {

            if (DeployAnim == "")
            {
                activated = true;
                automation = true;
            }
            else
            {
                isAnimating = true;
                isDeploying = true;
            }
            UpdateEventsDeployed(automation);
        }

        [KSPEvent(guiActive = true, guiName = Retract, active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void Retracting()
        {
            WantedRotation = retractedPos;
            activated = true;
            automation = false;
            if (DeployAnim != "")
            {
                isAnimating = true;
                isDeploying = false;
            }
            UpdateEventsDeployed(automation);
        }

        [KSPEvent(guiActive = true, guiName = RetractToggle, active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void RetractedPos()
        {
            if (retractedPos == retractedPos2)
            {
                retractedPos = retractedPos1;
            }
            else
            {
                retractedPos = retractedPos2;
            }
            WantedRotation = retractedPos;
            activated = true;
        }

        private void UpdateEventsDeployed(bool Activated)
        {
            Events["Deploying"].active = !Activated;
            Events["Retracting"].active = Activated;
        }

        [KSPAction(toggleDeploy, actionGroup = KSPActionGroup.Brakes)]
        public void ToggleRotate(KSPActionParam param)
        {
            activated = true;
            automation = !automation;
            if (automation == false)
            {
                WantedRotation = retractedPos;
            }
            UpdateEventsDeployed(automation);
        }

        [KSPAction(toggleRetracted)]
        public void ToggleRetracted(KSPActionParam param)
        {
            if (retractedPos == 0.0f)
            {
                retractedPos = 1.0f;
            }
            else
            {
                retractedPos = 0.0f;
            }
            WantedRotation = retractedPos;
            activated = true;
        }

        public void UpdateActuation(float dt)
        {
            if (Math.Abs(WantedRotation - CurrentRotation) < FlapSpeed * dt)
            {
                Flap.Rotate((WantedRotation - CurrentRotation) * Actuation, Space.Self);
                CurrentRotation = WantedRotation;
                if (!automation)
                {
                    activated = false;
                }
            }
            else if (WantedRotation > CurrentRotation)
            {
                Flap.Rotate(FlapSpeed * dt * Actuation, Space.Self);
                CurrentRotation += FlapSpeed * dt;
            }
            else
            {
                Flap.Rotate(-FlapSpeed * dt * Actuation, Space.Self);
                CurrentRotation -= FlapSpeed * dt;
            }

            //if (Math.Abs(WantedRotation - CurrentRotation) < FlapSpeed * dt)
            //{
            //    Flap.Rotate((WantedRotation - CurrentRotation) * Actuation, Space.Self);
            //    CurrentRotation = WantedRotation;
            //    Speed = 0;
            //    if (!automation)
            //    {
            //        activated = false;
            //    }
            //}
            //else
            //{
            //    float StopTime = Speed / FlapSpeed;
            //    if (Math.Abs(WantedRotation - CurrentRotation) < Math.Pow(Speed,2)*FlapSpeed/1.5)
            //    {
            //        if (Speed > 0)
            //            Speed -= FlapSpeed * dt;
            //        else
            //            Speed += FlapSpeed * dt;
            //    }
            //    else
            //    {
            //        if (Math.Abs(WantedRotation - CurrentRotation) > 0.5)
            //            Speed += FlapSpeed * dt * Math.Sign(WantedRotation - CurrentRotation);
            //        else
            //            Speed += FlapSpeed * dt * (WantedRotation - CurrentRotation) * 2;
            //
            //        this.Log(true, (Speed).ToString());
            //    }
            //    Flap.Rotate(Speed * dt * Actuation, Space.Self);
            //    CurrentRotation += Speed * dt;
            //}

            if (CoL != null)
            {
                part.CoLOffset = part.transform.InverseTransformPoint(CoL.position);
                part.CoPOffset = part.transform.InverseTransformPoint(CoL.position);
                if (changeCoM)
                {
                    part.CoMOffset = part.transform.InverseTransformPoint(CoL.position);
                }
            }
            else if (CoD != null)
            {
                part.CoLOffset = part.transform.InverseTransformPoint(CoD.position);
                part.CoPOffset = part.transform.InverseTransformPoint(CoD.position);
                if (changeCoM)
                {
                    part.CoMOffset = part.transform.InverseTransformPoint(CoD.position);
                }
            }
        }

        public float Rot2D(Vector3 dir, Vector3 comp, Vector3 normal)
        {
            float dist = new Plane(normal, Vector3.zero).GetDistanceToPoint(dir);

            dir = dir - (dist * normal);

            return Vector3.Angle(comp, dir);
        }

        public float groundEffect()
        {
            if (vessel.radarAltitude > 1000)
                return 1;

            extraheight = Vector3.Dot(vessel.graviticAcceleration.normalized, vessel.ReferenceTransform.position - CoL.position);

            float height = (float)vessel.radarAltitude + extraheight;
            float multiplier = 1 + 0.03f / (float)Math.Pow(height/WingLength , 1.5);
            return multiplier;
        }

        public (Vector3, Vector3) CalcLift(Vector3 velocity, Vector3 liftVelocity, Vector3 liftVector, Vector3 liftNormal)
        {
            Vector3 lift = Vector3.zero;
            Vector3 liftdrag = Vector3.zero;

            if (CoLName != "")
            {
                float angle = 0;
                float liftlength = 0;

                angle = Rot2D(velocity, liftVelocity, liftNormal);

                if (Rot2D(velocity, liftVector, liftNormal) > 90)
                    angle = -angle;

                lift = Vector3.Cross(velocity, liftNormal).normalized;
                if (Vector3.Angle(lift, liftVector) > 90)
                {
                    lift = -lift;
                }

                mach = part.machNumber;
                if (CLCurve != null)
                {
                    LiftCoeff = CLCurve.GetValue((float)mach);
                }
                if (LDCurve != null)
                {
                    LiftToDragRatio = LDCurve.GetValue(angle);
                }

                AoA = angle;
                liftlength = AoACurve.GetValue(angle) * LiftCoeff * groundEffect() * WingArea * (float)part.atmDensity * (float)Math.Pow(velocity.magnitude, 2) / 2;
                liftlength = liftlength / 1000; //N to kN

                lift = lift * liftlength;
                liftdrag = -velocity.normalized * Math.Abs(liftlength) / LiftToDragRatio;

                angle = Rot2D(vessel.up, liftVelocity, liftNormal);

                if (Rot2D(vessel.up, liftVector, liftNormal) > 90)
                    angle = -angle;
            }
            return (lift, liftdrag);
        }

        public Vector3 CalcDrag(Vector3 velocity, Transform transform = null)
        {
            Vector3 drag = Vector3.zero;

            if (CoDName != "")
            {
                mach = part.machNumber;

                if (CDnxCurve != null)
                {
                    finalDragCoefficients = new Vector3(
                        (velocity.x > 0) ? CDpxCurve.GetValue((float)mach) : CDnxCurve.GetValue((float)mach),
                        (velocity.x > 0) ? CDpyCurve.GetValue((float)mach) : CDnyCurve.GetValue((float)mach),
                        (velocity.x > 0) ? CDpzCurve.GetValue((float)mach) : CDnzCurve.GetValue((float)mach));
                }
                else
                {
                    finalDragCoefficients.x = (velocity.x > 0) ? PosDragCoefficients.x * CDCurve.GetValue((float)mach) : NegDragCoefficients.x * CDCurve.GetValue((float)mach);
                    finalDragCoefficients.y = (velocity.y > 0) ? PosDragCoefficients.y * CDCurve.GetValue((float)mach) : NegDragCoefficients.y * CDCurve.GetValue((float)mach);
                    finalDragCoefficients.z = (velocity.z > 0) ? PosDragCoefficients.z * CDCurve.GetValue((float)mach) : NegDragCoefficients.z * CDCurve.GetValue((float)mach);
                }

                drag.x = (finalDragCoefficients.x * DragArea.x * (float)part.atmDensity * (float)Math.Pow(velocity.x, 2)) / 2;
                drag.y = (finalDragCoefficients.y * DragArea.y * (float)part.atmDensity * (float)Math.Pow(velocity.y, 2)) / 2;
                drag.z = (finalDragCoefficients.z * DragArea.z * (float)part.atmDensity * (float)Math.Pow(velocity.z, 2)) / 2;

                drag.x = (velocity.x * drag.x > 0) ? -drag.x : drag.x;
                drag.y = (velocity.y * drag.y > 0) ? -drag.y : drag.y;
                drag.z = (velocity.z * drag.z > 0) ? -drag.z : drag.z;
                if (transform != null)
                    drag = transform.TransformDirection(drag);

                drag = drag / 1000; //N to kN
            }
            return drag;
        }

        public new void FixedUpdate()
        {
#if Timed
            timer.Start();
#endif

            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                return;
            }

            if (start)
            {
                foreach (PartModule mod in part.Modules)
                    if (updateRemoveModules.Contains(mod.ClassName))
                    {
                        part.RemoveModule(mod);
                        continue;
                    }
                start = false;
            }

            if (isAnimating && !activated)
            {
                if (Animation.Update(isDeploying ? 1 : 0, 1/DeployTime, 1/DeployTime) && isDeploying);
                {
                    activated = true;
                    automation = true;
                }
            }

            if (activated && (TimeWarp.CurrentRate == 1 || TimeWarp.WarpMode == TimeWarp.Modes.LOW))
            {
                mach = part.machNumber;
                if (automation)
                {
                    if (CenteredCurve != null)
                        defaultRot = CenteredCurve.GetValue((float)mach); //1 / (float)Math.Max(Math.Pow(mach, 0.5), 2);
                    else
                        defaultRot = 0.5f;

                    actuation.x = ((diffSaved.x < 0) ? 1 : -1);         //Debug
                    actuation.y = ((diffSaved.y < 0) ? 1 : -1);         //Debug
                    actuation.z = ((diffSaved.z < 0) ? 1 : -1);         //Debug

                    //WantedRotation = ((diffSaved.x < 0) ? 1 : -1) * vessel.ctrlState.pitch*PitchActuation;
                    //WantedRotation += ((diffSaved.y < 0) ? 1 : -1) * vessel.ctrlState.yaw*YawActuation;
                    //WantedRotation += ((diffSaved.z > 0) ? 1 : -1) * vessel.ctrlState.roll*RollActuation;

                    WantedRotation = vessel.ctrlState.pitch*PitchActuation * actuation.x;
                    WantedRotation += vessel.ctrlState.yaw*YawActuation * actuation.y;
                    WantedRotation += vessel.ctrlState.roll*RollActuation * actuation.z;

                    actuation = diffSaved;

                    WantedRotation = Math.Max(Math.Min(WantedRotation, 1), -1);
                    if (defaultRot <= 0.5)
                    {
                        WantedRotation = (WantedRotation * defaultRot) + 1f-defaultRot;
                    }
                    else
                    {
                        WantedRotation = (WantedRotation * (1f-defaultRot)) + 1f-defaultRot;
                    }

                    this.Log(true, "WantedRotation " + WantedRotation);

                    //WantedRotation = (Math.Max(Math.Min((vessel.ctrlState.pitch * PitchActuation) + (vessel.ctrlState.yaw * YawActuation) + (vessel.ctrlState.roll * RollActuation), 1), -1) * defaultRot) - defaultRot + 1f;
                }
                if (WantedRotation != CurrentRotation && Flap != null)
                {
                    UpdateActuation(TimeWarp.fixedDeltaTime);
                }
            }

            if (vessel.BestSituation.GetHashCode() < 16 && !part.ShieldedFromAirstream)
            {
                Vector3d velocity = new Vector3d();

                if (CoL != null)
                {
                    rotVel = ((CoL.position - vessel.CoM) - oldPosL) / TimeWarp.fixedDeltaTime;
                    velocity = vessel.srf_velocity + rotVel;
                    (lift, dragLift) = CalcLift(velocity, CoL.up, CoL.forward, CoL.right);

                    part.AddForceAtPosition((lift + dragLift), CoL.position);
                }
                if (CoD != null)
                {
                    rotVel = ((CoD.position - vessel.CoM) - oldPosD) / TimeWarp.fixedDeltaTime;
                    velocity = CoD.InverseTransformDirection(vessel.srf_velocity + rotVel);
                    drag = CalcDrag(velocity, CoD);

                    part.AddForceAtPosition(drag, CoD.position);
                }

                Vector3d totalForce = drag + dragLift + lift;
                Vector3d velVec = vessel.srf_velocity.normalized;
                DragLen = -Vector3.Dot(totalForce, vessel.srf_velocity.normalized);
                trueDrag = vessel.srf_velocity.normalized * -DragLen;
                trueLift = totalForce - trueDrag;
                LiftLen = trueLift.magnitude;
                LoD = LiftLen / DragLen;

                totalLine.Update((CoD == null) ? CoL.position : CoD.position, totalForce * 0.01f);
                dragLine.Update((CoD == null) ? CoL.position : CoD.position, trueDrag * 0.01f);
                liftLine.Update((CoL == null) ? CoD.position : CoL.position, trueLift * 0.01f);

                dragScalar = (float)DragLen;
                liftScalar = (float)LiftLen;
            }
            if (CoL != null)
                oldPosL = CoL.position - vessel.CoM;
            if (CoD != null)
                oldPosD = CoD.position - vessel.CoM;

#if Timed
            this.Log(true, $"{timer.Stop()} for FixedUpdate on {part.name}");
#endif
        }

        public (Vector3, Vector3) GetPotentialDragCoML(Vector3 actuation, float dt, float actuated = 0)
        {
            if (actuated == 0)
                actuated = (actuation.x * PitchActuation) + (actuation.y * YawActuation) + (actuation.z * RollActuation);
            float rot = Math.Max(Math.Min(actuated, 1), -1);
            if (defaultRot <= 0.5)
            {
                rot = (rot * defaultRot) + 1f - defaultRot;
            }
            else
            {
                rot = (rot * (1f - defaultRot)) + defaultRot;
            }

            Quaternion globRot = Quaternion.identity;
            globRot.eulerAngles = Actuation * (rot - CurrentRotation);
            Quaternion moveVector = Flap.rotation * globRot;

            Vector3 colPos = Vector3.zero;
            if (CoD != null)
            {
                colPos = CoD.position - Flap.position;
            }
            if (CoL != null)
            {
                colPos = CoL.position - Flap.position;
            }
            Vector3 newPos = Flap.position + (globRot * colPos) - vessel.CoM;
            Vector3 CoML = vessel.ReferenceTransform.InverseTransformDirection(newPos);

            Vector3 Drag = Vector3.zero;
            Vector3 Lift = Vector3.zero;
            Vector3 LiftDrag = Vector3.zero;

            if (CoD != null)
            {
                Vector3d rotVel = (newPos - oldPosD) / dt;
                Vector3 velocity = vessel.srf_velocity + rotVel;
                Vector3 TransVel = Quaternion.Inverse(moveVector * CoD.localRotation) * vessel.srf_velocity;

                Drag = CalcDrag(TransVel);
                Drag = (moveVector * CoD.localRotation) * Drag;
            }
            if (CoL != null)
            {
                Vector3d rotVel = (newPos - oldPosL) / dt;
                Vector3 velocity = vessel.srf_velocity + rotVel;
                Vector3 up =  moveVector * (Quaternion.Inverse(Flap.rotation) * CoL.up);
                Vector3 forward = moveVector * (Quaternion.Inverse(Flap.rotation) * CoL.forward);
                Vector3 right = moveVector * (Quaternion.Inverse(Flap.rotation) * CoL.right);

                (Lift, LiftDrag) = CalcLift(vessel.srf_velocity, up, forward, right);
            }
            Drag = vessel.ReferenceTransform.InverseTransformDirection(Drag + Lift + LiftDrag);

            //Drag = vessel.transform.InverseTransformDirection(Drag);

            return (Drag, CoML);
        }

        public void GetPotentialTorque(out Vector3 pos, out Vector3 neg)
        {
            pos = Vector3.zero;
            neg = Vector3.zero;
        
            if (!automation || vessel.BestSituation.GetHashCode() > 8)
                return;

            Vector3 CoMLp = Vector3.zero;
            Vector3 Dragp = Vector3.zero;
            Vector3 CoMLn = Vector3.zero;
            Vector3 Dragn = Vector3.zero;

            (Dragp, CoMLp) = GetPotentialDragCoML(Vector3.zero, (float)0.02, 1);
            (Dragn, CoMLn) = GetPotentialDragCoML(Vector3.zero, (float)0.02, -1);

            ld1Saved = Dragp;
            ld2Saved = Dragn;

            Vector3 tempPos = new Vector3();
            Vector3 tempNeg = new Vector3();
            tempPos.x = (Dragp.z * CoMLp.y - Dragp.y * CoMLp.z);
            tempPos.y = (Dragp.y * CoMLp.x - Dragp.x * CoMLp.y);
            tempPos.z = (Dragp.x * CoMLp.z - Dragp.z * CoMLp.x);

            tempNeg.x = (Dragn.z * CoMLn.y - Dragn.y * CoMLn.z);
            tempNeg.y = (Dragn.y * CoMLn.x - Dragn.x * CoMLn.y);
            tempNeg.z = (Dragn.x * CoMLn.z - Dragn.z * CoMLn.x);

            diffSaved = tempPos - tempNeg;

            pos.x = (tempPos.x+tempNeg.x)/2 - diffSaved.x*PitchActuation * (FlapSpeed) / 2;
            neg.x = (tempPos.x+tempNeg.x)/2 + diffSaved.x*PitchActuation * (FlapSpeed) / 2;
            pos.y = (tempPos.y+tempNeg.y)/2 - diffSaved.y*YawActuation * (FlapSpeed) / 2;
            neg.y = (tempPos.y+tempNeg.y)/2 + diffSaved.y*YawActuation * (FlapSpeed) / 2;
            pos.z = (tempPos.z+tempNeg.z)/2 - diffSaved.z*RollActuation * (FlapSpeed) / 2;
            neg.z = (tempPos.z+tempNeg.z)/2 + diffSaved.z*RollActuation * (FlapSpeed) / 2;

            //if (PitchActuation > 0)
            //{
            //    pos.x = (Dragp.z * CoMLp.y + Dragp.y * CoMLp.z) * PitchActuation;
            //    neg.x = (Dragn.z * CoMLn.y + Dragn.y * CoMLn.z) * PitchActuation;
            //}
            //else
            //{
            //    pos.x = (Dragn.z * CoMLn.y + Dragn.y * CoMLn.z) * PitchActuation;
            //    neg.x = (Dragp.z * CoMLp.y + Dragp.y * CoMLp.z) * PitchActuation;
            //}
            //if (YawActuation > 0)
            //{
            //    pos.y = (Dragp.x * CoMLp.y - Dragp.y * CoMLp.x) * YawActuation;
            //    neg.y = (Dragn.x * CoMLn.y - Dragn.y * CoMLn.x) * YawActuation;
            //}
            //else
            //{
            //    pos.y = (Dragn.x * CoMLn.y - Dragn.y * CoMLn.x) * YawActuation;
            //    neg.y = (Dragp.x * CoMLp.y - Dragp.y * CoMLp.x) * YawActuation;
            //}
            //if (RollActuation > 0)
            //{
            //    pos.z = -(Dragp.x * CoMLp.z - Dragp.z * CoMLp.x) * RollActuation;
            //    neg.z = -(Dragn.x * CoMLn.z - Dragn.z * CoMLn.x) * RollActuation;
            //}
            //else
            //{
            //    pos.z = -(Dragn.x * CoMLn.z - Dragn.z * CoMLn.x) * RollActuation;
            //    neg.z = -(Dragp.x * CoMLp.z - Dragp.z * CoMLp.x) * RollActuation;
            //}

            pos = Vector3.Scale(pos, scaleDrag);
            neg = Vector3.Scale(neg, scaleDrag);
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                if(FlapName == "")
                {
                    Actions["ToggleRotate"].active = false;
                    Actions["ToggleRetracted"].active = false;

                    Fields["PitchActuation"].guiActiveEditor = false;
                    Fields["YawActuation"].guiActiveEditor = false;
                    Fields["RollActuation"].guiActiveEditor = false;
                }
            
                Events["Deploying"].active = false;
                Events["Retracting"].active = false;
                Events["RetractedPos"].active = false;
            
                return;
            }

            ConfigNode config = new ConfigNode();
            List<ConfigNode> configs = this.GetConfigs(part.partInfo.partConfig);
            foreach (ConfigNode node in configs)
            {
                if (((node.GetValue("CoDName") == null && CoDName == "") || node.GetValue("CoDName") == CoDName) &&
                ((node.GetValue("CoLName") == null && CoLName == "") || node.GetValue("CoLName") == CoLName))
                {
                    config = node;
                    break;
                }
            }

            foreach (ConfigNode node in config.nodes.GetNodes("CenteredCurve"))
            {
                CenteredCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CenteredCurve.AddPoint(String);
            }
            if (CenteredCurve != null)
                CenteredCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("AoACurve"))
            {
                AoACurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    AoACurve.AddPoint(String);
            }
            if (AoACurve != null)
                AoACurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDnxCurve"))
            {
                CDnxCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDnxCurve.AddPoint(String);
            }
            if (CDnxCurve != null)
                CDnxCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDnyCurve"))
            {
                CDnyCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDnyCurve.AddPoint(String);
            }
            if (CDnyCurve != null)
                CDnyCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDnzCurve"))
            {
                CDnzCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDnzCurve.AddPoint(String);
            }
            if (CDnzCurve != null)
                CDnzCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDpxCurve"))
            {
                CDpxCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDpxCurve.AddPoint(String);
            }
            if (CDpxCurve != null)
                CDpxCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDpyCurve"))
            {
                CDpyCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDpyCurve.AddPoint(String);
            }
            if (CDpyCurve != null)
                CDpyCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDpzCurve"))
            {
                CDpzCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDpzCurve.AddPoint(String);
            }
            if (CDpzCurve != null)
                CDpzCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CDCurve"))
            {
                CDCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CDCurve.AddPoint(String);
            }
            if (CDCurve != null)
                CDCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("CLCurve"))
            {
                CLCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    CLCurve.AddPoint(String);
            }
            if (CLCurve != null)
                CLCurve.SortCurve();

            foreach (ConfigNode node in config.nodes.GetNodes("LDCurve"))
            {
                LDCurve = new Curve();
                string[] strings = node.GetValues("key");
                foreach (string String in strings)
                    LDCurve.AddPoint(String);
            }
            if (LDCurve != null)
                LDCurve.SortCurve();

            retractedPos = retractedPos1;
            if (retractedPos2 == 0)
                Events["RetractedPos"].active = false;

            if (CoDName != "")
            {
                CoD = part.FindModelTransform(CoDName);
                oldPosD = CoD.position;
                if (FlapName != "")
                {
                    Flap = part.FindModelTransform(FlapName);
                    part.CoPOffset = (CoD.position - Flap.position);
                    if (changeCoM)
                    {
                        part.CoMOffset = (CoD.position - Flap.position);
                    }
                }
            }
            if (CoLName != "")
            {
                CoL = part.FindModelTransform(CoLName);
                CoL.localPosition += LiftShift;
                oldPosL = CoL.position;
                if (FlapName != "")
                {
                    Flap = part.FindModelTransform(FlapName);
                    part.CoLOffset = (CoL.position - Flap.position);
                    if (changeCoM)
                    {
                        part.CoMOffset = (CoL.position - Flap.position);
                    }
                }
            }

            automation = activated;

            if (CDnxCurve == null)
            {
                CDCurve = new Curve();
                CDCurve.AddPoint("0.0 1.0");
                CDCurve.AddPoint("0.8 1.2");
                CDCurve.AddPoint("1.0 4.5");
                CDCurve.AddPoint("1.2 3.0");
                CDCurve.AddPoint("2.0 2");
                CDCurve.AddPoint("5.0 1.5");
                CDCurve.AddPoint("10.0 1.5");
            }

            if (FlapName == "")
            {
                Events["Deploying"].active = false;
                Events["Retracting"].active = false;
                Events["RetractedPos"].active = false;

                Actions["ToggleRotate"].active = false;
                Actions["ToggleRetracted"].active = false;  

                Fields["PitchActuation"].guiActive = false;
                Fields["YawActuation"].guiActive = false;
                Fields["RollActuation"].guiActive = false;
                Fields["PitchActuation"].guiActiveEditor = false;
                Fields["YawActuation"].guiActiveEditor = false;
                Fields["RollActuation"].guiActiveEditor = false;

                Fields["LoD"].guiActive = false;
                Fields["actuation"].guiActive = false;
            }
            if (CoL == null)
                Fields["AoA"].guiActive = false;
            else
            {
                UpdateEventsDeployed(activated);
            }

            WantedRotation = CurrentRotation;

            totalLine = new RenderLine(part, "total", Color.green);
            liftLine = new RenderLine(part, "lift", Color.blue);
            dragLine = new RenderLine(part, "drag", Color.red);

            totalLine.setActive(debug);
            liftLine.setActive(debug);
            dragLine.setActive(debug);

            if (DeployAnim != "")
                Animation = new AnimationHelper(DeployAnim, part);

            DragArea *= part.scaleFactor;
            WingArea *= (float)Math.Pow(part.scaleFactor, 2);

            //FlapSpeed *= 2;

            base.OnStart(state);
        }
    }
}
