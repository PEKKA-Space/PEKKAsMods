using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutomatedCoMShifter
{
    public class AutomatedCoMShifter : PartModule
    {
        public const string groupName = "AutomatedCoMShifter";

        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public float AutomatedPitch = 0.1f;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public float AutomatedYaw = 0;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public float AutomatedRoll = 0;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public Vector3 CoMchangePitch = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public Vector3 CoMchangeYaw = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public Vector3 CoMchangeRoll = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public Vector3 DefaultCoM = Vector3.zero;
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public Vector3 CoM2 = Vector3.zero;
        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiFormat = "P0", guiName = "Current Pitch Offset", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = -1, maxValue = 1, stepIncrement = 0.01f)]
        public float CoMchangedPitch = 0;
        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiFormat = "P0", guiName = "Current Yaw Offset", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = -1, maxValue = 1, stepIncrement = 0.01f)]
        public float CoMchangedYaw = 0;
        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiFormat = "P0", guiName = "Current Roll Offset", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = -1, maxValue = 1, stepIncrement = 0.01f)]
        public float CoMchangedRoll = 0;


        const string toggleCoM = "Toggle CoM Location";
        const string toggleAutomation = "Toggle CoM Shifting";
        const string hideAutomation = "Manual CoM Shifting";
        const string showAutomation = "Automate CoM Shifting";
        const string resyncing = "Update CoM";
        float currentpitch = 0;
        float currentyaw = 0;
        float currentroll = 0;
        bool StartCoM = true;
        bool automation = false;
        Vector3 StartingCoM;
        Vector3 CoM;

        [KSPEvent(guiActive = true, guiName = resyncing, active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void resync()
        {
            if (StartCoM)
            {
                CoM = StartingCoM + DefaultCoM;
            }
            if (!StartCoM)
            {
                CoM = StartingCoM + CoM2;
            }
            if ((CoMchangedPitch != 0 || CoMchangedYaw != 0 || CoMchangedRoll != 0) && StartCoM)
            {
                part.CoMOffset = CoM + CoMchangePitch * CoMchangedPitch + CoMchangeYaw * CoMchangedYaw + CoMchangeRoll * CoMchangedRoll;
            }
            if (CoMchangedPitch == 0 && CoMchangedYaw == 0 && CoMchangedRoll == 0)
            {
                part.CoMOffset = StartingCoM;
            }
        }

        [KSPEvent(guiActive = true, guiName = showAutomation, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void Automated()
        {
            automation = true;
            Activated();
            UpdateEvents(automation);
        }

        [KSPEvent(guiActive = true, guiName = hideAutomation, active = false, groupName = groupName, groupDisplayName = groupName)]
        public void Manual()
        {
            automation = false;
            UpdateEvents(automation);
        }
        
        private void UpdateEvents(bool Automated)
        {
            Events["Automated"].active = !Automated;
            Events["Manual"].active = Automated;
        }

        [KSPAction(toggleAutomation)]
        public void ToggleAutomation(KSPActionParam param)
        {
            automation = !automation;
            if (automation)
            {
                Activated();
            }
            UpdateEvents(automation);
        }

        [KSPAction(toggleCoM)]
        public void ToggleCoM(KSPActionParam param)
        {
            StartCoM = !StartCoM;
        }

        protected void Shifting()
        {
            if (StartCoM)
            {
                CoM = StartingCoM + DefaultCoM;
            }
            if (!StartCoM)
            {
                CoM = StartingCoM + CoM2;
            }
            currentpitch = vessel.ctrlState.pitch;
            currentyaw = vessel.ctrlState.yaw;
            currentroll = vessel.ctrlState.roll;
            if (currentpitch < AutomatedPitch && CoMchangedPitch < 1 && CoMchangePitch != Vector3.zero)
            {
                CoMchangedPitch = CoMchangedPitch + 0.01f;
            }
            if (currentpitch > -AutomatedPitch && CoMchangedPitch > -1 && CoMchangePitch != Vector3.zero)
            {
                CoMchangedPitch = CoMchangedPitch - 0.01f;
            }
            if (currentyaw < AutomatedYaw && CoMchangedYaw < 1 && CoMchangeYaw != Vector3.zero)
            {
                CoMchangedYaw = CoMchangedYaw + 0.01f;
            }
            if (currentyaw > -AutomatedYaw && CoMchangedYaw > -1 && CoMchangeYaw != Vector3.zero)
            {
                CoMchangedYaw = CoMchangedYaw - 0.01f;
            }
            if (currentroll < AutomatedRoll && CoMchangedRoll < 1 && CoMchangeRoll != Vector3.zero)
            {
                CoMchangedRoll = CoMchangedRoll + 0.01f;
            }
            if (currentroll > -AutomatedRoll && CoMchangedRoll > -1 && CoMchangeRoll != Vector3.zero)
            {
                CoMchangedRoll = CoMchangedRoll - 0.01f;
            }
            part.CoMOffset = CoM + CoMchangePitch * CoMchangedPitch + CoMchangeYaw * CoMchangedYaw + CoMchangeRoll * CoMchangedRoll;
            if (!automation)
            {
                part.CoMOffset = StartingCoM;
                CancelInvoke("Shifting");
            }
        }

        void Activated()
        {
            InvokeRepeating("Shifting", 0, 0.1F);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            StartingCoM = part.CoMOffset;
            UpdateEvents(automation);
        }
    }
}
