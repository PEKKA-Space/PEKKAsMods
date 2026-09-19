
#undef Timed

using PEKKAUtils;
using System.Collections.Generic;
using UnityEngine;

namespace ModulePekkaFrost
{
    public class Prop
    {
        public PartResource Propellant = null;
        public PartResource OtherProp = null;
        public AnimationHelper Animation = null;
        public Material PropMaterial = null;
        public float FillTime = 0f;
        public float DrainTime = 0f;
        public Curve FrostCurve = null;
        public Curve OtherCurve = null;

        public float minNoise = 0f;
        public float negPosition = 0f;

        public bool isAnimating = false;
        public float animationTime = 0f;
        public float oldSpeed = 0f;
        public float oldNegativeSpeed = 0f;
        public float position = 0f;
        public float oldPosition = 0f;
        public float minChange = 0f;

        public float CalcPosition()
        {
            return FrostCurve.GetValue((float)(Propellant.amount / Propellant.maxAmount));
        }
        public float CalcOtherPosition()
        {
            if (OtherCurve != null)
                return OtherCurve.GetValue((float)(OtherProp.amount / OtherProp.maxAmount));
            return 0;
        }

        public void ControllAnimation(float speed, float negativespeed)
        {
            position = CalcPosition();
            minChange = CalcOtherPosition();
            float upper = 0;
            float noise = 0;
            if ((oldPosition - TimeWarp.fixedDeltaTime / DrainTime) > position)
            {
                oldPosition -= TimeWarp.fixedDeltaTime / DrainTime;
                upper = oldPosition;
                noise = oldPosition - position;
            }
            else if ((oldPosition + TimeWarp.fixedDeltaTime / FillTime) < position)
            {
                oldPosition += TimeWarp.fixedDeltaTime / FillTime;
                upper = position;
                noise = position - oldPosition;
            }
            else
            {
                oldPosition = position;
                upper = position;
                noise = position - oldPosition;
            }

            PropMaterial.SetFloat("_MainRevealValue", upper);
            if (position > (1 - minNoise))
                PropMaterial.SetFloat("_MainNoiseMax", (noise > (1 - position)) ? noise : (1 - position));
            else
                PropMaterial.SetFloat("_MainNoiseMax", (noise > minNoise) ? noise : minNoise);

            PropMaterial.SetFloat("_MainHideValue", ((upper > 0.1f) ? negPosition : upper*10 * negPosition) + minChange);
            PropMaterial.SetFloat("_MainNoiseMin", (upper > 0.1f) ? negPosition : upper*10 * negPosition);
            PropMaterial.SetFloat("_SecHideValue", ((upper > 0.1f) ? negPosition : upper*10 * negPosition) + minChange);
            PropMaterial.SetFloat("_SecNoiseMin", (upper > 0.1f) ? negPosition : upper*10 * negPosition);

            PropMaterial.SetFloat("_SecRevealValue", position);
            PropMaterial.SetFloat("_SecNoiseMax", (minNoise*2));

            Animation.Update(oldPosition, speed / FillTime, negativespeed / DrainTime);
        }
    }

    public class ModulePekkaFrost : PartModule
    {
        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "P0", guiName = "Speed", groupName = groupName, groupDisplayName = groupName)]
        //[UI_FloatRange(minValue = 0.05f, maxValue = 2f, stepIncrement = 0.05f)]
        public float Speed = 1.0f;
        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "P0", guiName = "NegativeSpeed", groupName = groupName, groupDisplayName = groupName)]
        //[UI_FloatRange(minValue = 0.05f, maxValue = 2, stepIncrement = 0.05f)]
        public float NegativeSpeed = 1.0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "P0", guiName = "Drain to", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f)]
        public float Drain = 1.0f;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "N2", guiName = "Drain speed", groupName = groupName, groupDisplayName = groupName, guiUnits = "%/s")]
        [UI_FloatRange(minValue = 0.01f, maxValue = 1, stepIncrement = 0.01f)]
        public float DrainSpeed = 1.0f;
        public bool isDraining = false;

        public const string groupName = "PekkaFrost";

        public List<Prop> Propellants = new List<Prop>();

#if Timed
        PerfTimer timer = new PerfTimer();
#endif

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Start Draining", active = true, groupName = groupName, groupDisplayName = groupName, guiActiveUnfocused = true, unfocusedRange = 100)]
        protected void DrainStart()
        {
            isDraining = true;

            Events["DrainStart"].active = false;
        }

        public void FixedUpdate()
        {
#if Timed
            timer.Start();
#endif
            foreach (Prop prop in Propellants)
            {
                prop.ControllAnimation(Speed, NegativeSpeed);
            }

            if (isDraining)
            {
                bool disable = true;
                foreach (Prop prop in Propellants)
                    if (prop.Propellant.amount / prop.Propellant.maxAmount > Drain)
                    {
                        prop.Propellant.amount -= DrainSpeed * TimeWarp.fixedDeltaTime * prop.Propellant.maxAmount / 100;

                        if (prop.Propellant.amount / prop.Propellant.maxAmount < Drain)
                            prop.Propellant.amount = Drain * prop.Propellant.maxAmount;

                        disable = false;
                    }
                if (disable)
                {
                    isDraining = false;
                    Events["DrainStart"].active = true;
                }
            }

#if Timed
            this.Log(true, $"{timer.Stop()} for FixedUpdate on {part.name}");
#endif
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            PartResourceList PartResources = part.Resources;

            ConfigNode config = this.GetConfig(part.partInfo.partConfig);

            foreach (ConfigNode node in config.nodes.GetNodes("PROPELLANT"))
            {
                Prop newProp = new Prop();
                newProp.Animation =  new AnimationHelper(node.GetValue("AnimationName"), part);
                newProp.Propellant = PartResources.Get(node.GetValue("Propellant"));
                if (node.GetValue("OtherProp") != null)
                {
                    newProp.OtherProp = PartResources.Get(node.GetValue("OtherProp"));
                }
                newProp.FillTime = float.Parse(node.GetValue("FillTime"));
                newProp.DrainTime = float.Parse(node.GetValue("DrainTime"));
                newProp.PropMaterial = part.FindModelTransform(node.GetValue("propName")).GetComponent<MeshRenderer>().material;

                newProp.minNoise = float.Parse(node.GetValue("MinNoise"));
                newProp.negPosition = float.Parse(node.GetValue("NegPosition"));

                foreach (ConfigNode curve in node.GetNodes("FrostCurve"))
                {
                    newProp.FrostCurve = new Curve();
                    string[] strings = curve.GetValues("key");
                    foreach (string String in strings)
                        newProp.FrostCurve.AddPoint(String);
                }
                newProp.FrostCurve.SortCurve();

                foreach (ConfigNode curve in node.GetNodes("OtherCurve"))
                {
                    newProp.OtherCurve = new Curve();
                    string[] strings = curve.GetValues("key");
                    foreach (string String in strings)
                        newProp.OtherCurve.AddPoint(String);
                }
                if (newProp.OtherCurve != null)
                    newProp.OtherCurve.SortCurve();

                Propellants.Add(newProp);
            }

            if (state == StartState.Editor)
            {
                foreach (Prop prop in Propellants)
                {
                    prop.FillTime /= 20;
                    prop.DrainTime /= 20;
                }
            }
        }
    }
}
