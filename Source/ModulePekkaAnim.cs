
#undef Timed

using System;
using PEKKAUtils;

namespace ModulePekkaAnim
{
    public class ModulePekkaAnim : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "N2", guiName = "Position", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f)]
        public float Position = 1.0f;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "N2", guiName = "Speed", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f)]
        public float Speed = 1.0f;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiFormat = "N2", guiName = "Difference", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f)]
        public float Difference = 0f;

        public const string groupName = "PekkaAnim";

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool debug = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Name = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Units = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Different1 = "Animation";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Different2 = "Animation";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Same1 = "Animation";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Same2 = "Animation";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float minDifference = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float maxDifference = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float minPosition = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float maxPosition = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float minSpeed = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float maxSpeed = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float Start1Position = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float Start2Position = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Name1 = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float Position1 = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float Difference1 = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string Name2 = "";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float Position2 = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float Difference2 = 0f;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string NameToggle = "";

        //public bool automation = false;
        //public bool Animation1 = false;
        //public bool Animation2 = false;
        //public float Animation1Time = 0f;
        //public float Animation2Time = 0f;
        //public float Animation1TimeWanted = 0f;
        //public float Animation2TimeWanted = 0f;
        public float Animation1PositionWanted = 0f;
        public float Animation2PositionWanted = 0f;
        //public float oldAnimation1Speed = 0f;
        //public float oldAnimation2Speed = 0f;
        public float oldPosition = 0f;
        public float oldDifference = 0f;
        //public Animation Different1Animation;
        //public Animation Different2Animation;
        //public Animation Same1Animation;
        //public Animation Same2Animation;
        //public int count;
        //public int currentstate = 1;
        //public StartState oldstate;
        public bool Position1Bool = true;

        public AnimationHelper Different1AnimationHelper;
        public AnimationHelper Different2AnimationHelper;
        public AnimationHelper Same1AnimationHelper;
        public AnimationHelper Same2AnimationHelper;

#if Timed
        PerfTimer timer = new PerfTimer();
#endif

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Position1", active = true, groupName = groupName, groupDisplayName = groupName, guiActiveUnfocused = false, unfocusedRange = 100)]
        protected void Position1Start()
        {
            Position = Position1;
            Difference = Difference1;
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Position2", active = true, groupName = groupName, groupDisplayName = groupName, guiActiveUnfocused = false, unfocusedRange = 100)]
        protected void Position2Start()
        {
            Position = Position2;
            Difference = Difference2;
        }

        [KSPAction(guiName = groupName)]
        public void PositionToggle(KSPActionParam param)
        {
            if (Position1Bool)
            {
                Position = Position1;
                Difference = Difference1;
            }
            else
            {
                Position = Position2;
                Difference = Difference2;
            }
            Position1Bool = !Position1Bool;
        }

        void SetupUI()
        {
            Fields.TryGetFieldUIControl("Position", out UI_FloatRange PositionField);
            Fields.TryGetFieldUIControl("Speed", out UI_FloatRange SpeedField);
            Fields.TryGetFieldUIControl("Difference", out UI_FloatRange DifferenceField);
            if (Units != "")
            {
                Fields["Position"].guiUnits = Units;
                Fields["Speed"].guiUnits = Units + "/s";
                Fields["Difference"].guiUnits = Units;
            }

            PositionField.maxValue = maxPosition;
            PositionField.minValue = minPosition;
            if (maxPosition - minPosition > 100)
            {
                PositionField.stepIncrement = 0.1f;
            }

            SpeedField.maxValue = maxSpeed;
            SpeedField.minValue = minSpeed;
            if (maxSpeed - minSpeed > 100)
            {
                PositionField.stepIncrement = 0.1f;
            }

            if (Different2 == "Animation")
            {
                Fields["Difference"].guiActive = false;
                Fields["Difference"].guiActiveEditor = false;
            }
            else
            {
                PositionField.maxValue = maxPosition / 2 - minPosition / 2;
                PositionField.minValue = -maxPosition / 2 + minPosition / 2;
                if (maxPosition -minPosition > 100)
                {
                    PositionField.stepIncrement = 0.1f;
                }

                DifferenceField.maxValue = maxPosition * 2;
                DifferenceField.minValue = minDifference;
                if (maxPosition * 2 - minDifference > 100)
                {
                    DifferenceField.stepIncrement = 0.1f;
                }
                Fields["Difference"].guiName = Name + " Difference";
            }

            Speed = maxSpeed;
            Fields["Position"].guiName = Name + " Position";
            Fields["Speed"].guiName = Name + " Speed";
            Events["Position1Start"].guiName = Name1;
            Events["Position2Start"].guiName = Name2;
            Actions["PositionToggle"].guiName = NameToggle;

            if (Position == Position1 && Difference == Difference1)
                Position1Bool = false;

            if (NameToggle == "")
            {
                Actions["PositionToggle"].active = false;
            }

            if ((Position == Position1 && Difference == Difference1) || Name1 == "")
            {
                Events["Position1Start"].active = false;
            }
            else
            {
                Events["Position1Start"].active = true;
            }

            if ((Position == Position2 && Difference == Difference2) || Name2 == "")
            {
                Events["Position2Start"].active = false;
            }
            else
            {
                Events["Position2Start"].active = true;
            }

        }

        //public void StartAnimation()
        //{
        //    newTime();
        //
        //    Different1Animation[Different1].speed = 1f;
        //    Different1Animation[Different1].time = Animation1TimeWanted - 0.2f;
        //    Different1Animation[Different1].enabled = true;
        //    Different1Animation.Play(Different1);
        //    Animation1 = true;
        //    oldAnimation1Speed = maxSpeed;
        //
        //    if (Different2 != "Animation")
        //    {
        //        Different2Animation[Different2].speed = 1f;
        //        Different2Animation[Different2].time = Animation2TimeWanted - 0.2f;
        //        Different2Animation[Different2].enabled = true;
        //        Different2Animation.Play(Different2);
        //        Animation2 = true;
        //        oldAnimation2Speed = maxSpeed;
        //    }
        //    if (Same1 != "Animation")
        //    {
        //        Same1Animation[Same1].speed = 1f;
        //        Same1Animation[Same1].time = Animation1TimeWanted - 0.2f;
        //        Same1Animation[Same1].enabled = true;
        //        Same1Animation.Play(Same1);
        //    }
        //    if (Same2 != "Animation")
        //    {
        //        Same2Animation[Same2].speed = 1f;
        //        Same2Animation[Same2].time = Animation2TimeWanted - 0.2f;
        //        Same2Animation[Same2].enabled = true;
        //        Same2Animation.Play(Same2);
        //    }
        //}

        void newTime()
        {
            if (Different2 != "Animation")
            {
                if (Position + Difference / 2 < minPosition)
                {
                    if (Position != oldPosition)
                    {
                        Difference = 2 * (minPosition - Position);
                    }
                    if (Difference != oldDifference)
                    {
                        Position = minPosition - Difference / 2;
                    }
                }

                if (-Position + Difference / 2 < minPosition)
                {
                    if (Position != oldPosition)
                    {
                        Difference = 2 * (minPosition + Position);
                    }
                    if (Difference != oldDifference)
                    {
                        Position = -minPosition + Difference / 2;
                    }
                }

                if (Position + Difference / 2 > maxPosition)
                {
                    if (Position != oldPosition)
                    {
                        Difference = 2 * (maxPosition - Position);
                    }
                    else
                    {
                        Position = maxPosition - Difference / 2;
                    }
                }

                if (-Position + Difference / 2 > maxPosition)
                {
                    if (Position != oldPosition)
                    {
                        Difference = 2 * (maxPosition + Position);
                    }
                    else
                    {
                        Position = -maxPosition + Difference / 2;
                    }
                }
                Animation2PositionWanted = Math.Abs(-Position + Difference / 2 - Start1Position) / (maxPosition - minPosition);
                //Animation2TimeWanted = Math.Abs(Animation2PositionWanted - Start2Position) / (maxPosition - minPosition) * Different2Animation[Different2].length;
            }

            Animation1PositionWanted = Math.Abs(Position + Difference / 2 - Start1Position) / (maxPosition - minPosition);
            //Animation1TimeWanted = Math.Abs(Position + Difference / 2 - Start1Position) / (maxPosition - minPosition) * Different1Animation[Different1].length;

            if ((Position == Position1 && Difference == Difference1) || Name1 == "")
            {
                Events["Position1Start"].active = false;
            }
            else
            {
                Events["Position1Start"].active = true;
            }

            if ((Position == Position2 && Difference == Difference2) || Name2 == "")
            {
                Events["Position2Start"].active = false;
            }
            else
            {
                Events["Position2Start"].active = true;
            }

            oldPosition = Position;
            oldDifference = Difference;
        }

        //void Animation1Start()
        //{
        //    if (Math.Abs(Animation1Time - Animation1TimeWanted) > 0.05 * Different1Animation[Different1].length * Speed / (maxPosition - minPosition))
        //    {
        //        if (Animation1TimeWanted > Animation1Time)
        //        {
        //            Different1Animation[Different1].speed = Different1Animation[Different1].length * Speed / (maxPosition - minPosition);
        //            if (Same1 != "Animation")
        //            {
        //                Same1Animation[Same1].speed = Same1Animation[Same1].length * Speed / (maxPosition - minPosition);
        //            }
        //            oldAnimation1Speed = Speed;
        //        }
        //        else if (Animation1TimeWanted < Animation1Time)
        //        {
        //            Different1Animation[Different1].speed = -Different1Animation[Different1].length * Speed / (maxPosition - minPosition);
        //            if (Same1 != "Animation")
        //            {
        //                Same1Animation[Same1].speed = -Same1Animation[Same1].length * Speed / (maxPosition - minPosition);
        //            }
        //            oldAnimation1Speed = -Speed;
        //        }
        //        Different1Animation[Different1].time = Animation1Time;
        //        Different1Animation[Different1].enabled = true;
        //        Different1Animation.Play(Different1);
        //        if (Same1 != "Animation")
        //        {
        //            Same1Animation[Same1].time = Animation1Time;
        //            Same1Animation[Same1].enabled = true;
        //            Same1Animation.Play(Same1);
        //        }
        //        Animation1 = true;
        //    }
        //}
        //
        //void Animation2Start()
        //{
        //    if (Math.Abs(Animation2Time - Animation2TimeWanted) > 0.05 * Different2Animation[Different2].length * Speed / (maxPosition - minPosition))
        //    {
        //        if (Animation2TimeWanted > Animation2Time)
        //        {
        //            Different2Animation[Different2].speed = Different2Animation[Different2].length * Speed / (maxPosition - minPosition);
        //            if (Same2 != "Animation")
        //            {
        //                Same2Animation[Same2].speed = Same2Animation[Same2].length * Speed / (maxPosition - minPosition);
        //            }
        //            oldAnimation2Speed = Speed;
        //        }
        //        else if (Animation2TimeWanted < Animation2Time)
        //        {
        //            Different2Animation[Different2].speed = -Different2Animation[Different2].length * Speed / (maxPosition - minPosition);
        //            if (Same2 != "Animation")
        //            {
        //                Same2Animation[Same2].speed = -Same2Animation[Same2].length * Speed / (maxPosition - minPosition);
        //            }
        //            oldAnimation2Speed = -Speed;
        //        }
        //        Different2Animation[Different2].time = Animation2Time;
        //        Different2Animation[Different2].enabled = true;
        //        Different2Animation.Play(Different2);
        //        if (Same2 != "Animation")
        //        {
        //            Same2Animation[Same2].time = Animation2Time;
        //            Same2Animation[Same2].enabled = true;
        //            Same2Animation.Play(Same2);
        //        }
        //        Animation2 = true;
        //    }
        //}
        //
        //void Animation1Stop()
        //{
        //    if (((Different1Animation[Different1].time >= Animation1TimeWanted || Speed != oldAnimation1Speed) && oldAnimation1Speed > 0) || ((Different1Animation[Different1].time <= Animation1TimeWanted || Speed != -oldAnimation1Speed) && oldAnimation1Speed < 0))
        //    {
        //        Animation1Time = Different1Animation[Different1].time;
        //        Different1Animation.Stop(Different1);
        //        if (Same1 != "Animation")
        //        {
        //            Same1Animation.Stop(Same1);
        //        }
        //        Animation1 = false;
        //    }
        //    if (Animation1TimeWanted >= Different1Animation[Different1].length && Different1Animation[Different1].enabled == false)
        //    {
        //        Animation1Time = Different1Animation[Different1].length;
        //        Animation1 = false;
        //
        //    }
        //}
        //
        //void Animation2Stop()
        //{
        //    if (((Different2Animation[Different2].time >= Animation2TimeWanted || Speed != oldAnimation2Speed) && oldAnimation2Speed > 0) || ((Different2Animation[Different2].time <= Animation2TimeWanted || Speed != -oldAnimation2Speed) && oldAnimation2Speed < 0))
        //    {
        //        Animation2Time = Different2Animation[Different2].time;
        //        Different2Animation.Stop(Different2);
        //        if (Same2 != "Animation")
        //        {
        //            Same2Animation.Stop(Same2);
        //        }
        //        Animation2 = false;
        //    }
        //    if (Animation2TimeWanted >= Different2Animation[Different2].length && Different2Animation[Different2].enabled == false)
        //    {
        //        Animation2Time = Different2Animation[Different2].length;
        //        Animation2 = false;
        //
        //    }
        //}

        public void FixedUpdate()
        {
#if Timed
            timer.Start();
#endif

            if (Position != oldPosition || Difference != oldDifference)
            {
                newTime();
            }

            Different1AnimationHelper.Update(Animation1PositionWanted, Speed / (maxPosition-minPosition), Speed / (maxPosition-minPosition), debug);
            if (Different2AnimationHelper != null)
                Different2AnimationHelper.Update(Animation2PositionWanted, Speed / (maxPosition-minPosition), Speed / (maxPosition-minPosition), debug);
            if (Same1AnimationHelper != null)
                Same1AnimationHelper.Update(Animation1PositionWanted, Speed / (maxPosition-minPosition), Speed / (maxPosition-minPosition), debug);
            if (Same2AnimationHelper != null)
                Same2AnimationHelper.Update(Animation2PositionWanted, Speed / (maxPosition-minPosition), Speed / (maxPosition-minPosition), debug);

            //if (currentstate == 1) //On Start but happens on FixedUpdate
            //{
            //    StartAnimation();
            //    currentstate = 0;
            //}
            //
            //if (Animation1 == false)
            //{
            //    Animation1Start();
            //}
            //else
            //{
            //    Animation1Stop();
            //}
            //if (Animation2 == false && Different2 != "Animation")
            //{
            //    Animation2Start();
            //}
            //if (Animation2)
            //{
            //    Animation2Stop();
            //}
            //

#if Timed
            this.Log(true, $"{timer.Stop()} for FixedUpdate on {part.name}");
#endif
        }

        //public override void OnLoad(ConfigNode TestNode)
        //{
        //    currentstate = 1;
        //}

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            Start();
        }

        public void Start()
        {
            //Different1Animation = part.FindModelAnimator(Different1);
            //Different1Animation.Stop(Different1);
            //if (Different2 != "Animation")
            //{
            //    Different2Animation = part.FindModelAnimator(Different2);
            //    Different2Animation.Stop(Different2);
            //}
            //if (Same1 != "Animation")
            //{
            //    Same1Animation = part.FindModelAnimator(Same1);
            //    Same1Animation.Stop(Same1);
            //}
            //if (Same2 != "Animation")
            //{
            //    Same2Animation = part.FindModelAnimator(Same2);
            //    Same2Animation.Stop(Same2);
            //}

            Different1AnimationHelper = new AnimationHelper(Different1, part);
            if (Different2 != "Animation")
                Different2AnimationHelper = new AnimationHelper(Different2, part);
            if (Same1 != "Animation")
                Same1AnimationHelper = new AnimationHelper(Same1, part);
            if (Same2 != "Animation")
                Same2AnimationHelper = new AnimationHelper(Same2, part);

            SetupUI();
            newTime();
        }
    }
}
