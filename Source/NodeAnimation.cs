using System;
using System.Collections.Generic;
using UnityEngine;

namespace NodeAnimation
{
    public class NodeAnimation : PartModule
    {
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public string Xtransform = "1 0";
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public string Ytransform = "1 0";
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public string Ztransform = "1 0";
        [KSPField(guiActiveEditor = false, isPersistant = true)]
        public string time = "1";

        public const string groupName = "NodeAnimator";
        const string Animation = "Animate Nodes";
        const string Stopping = "Stop Nodes";
        bool Animated = false;
        bool Deactivate = false;
        bool Negative = false;
        bool Float = false;
        int i = 0;
        int[] Xlength = { 0, 0, 0, 0, 0, 0 };
        int[] Ylength = { 0, 0, 0, 0, 0, 0 };
        int[] Zlength = { 0, 0, 0, 0, 0, 0 };
        int timelength = 0;
        int step = 1;
        float Xstep = 0;
        float Ystep = 0;
        float Zstep = 0;
        int steptime = 0;
        int Xactive = 0;
        int Yactive = 0;
        int Zactive = 0;

        Vector3 dp = Vector3.zero;
        Vector3 dc = Vector3.zero;
        Vector3 dr = Vector3.zero;
        Vector3 attachedposition = Vector3.zero;
        Vector3 currentposition = Vector3.zero;
        Vector3 attachedoriginalposition = Vector3.zero;

        List<AttachNode> node;
        [KSPEvent(guiActiveEditor = true, guiActive = true, guiName = Animation, active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void Animate()
        {
            i = 0;
            Xlength[0] = 0;
            Xlength[1] = 0;
            Xlength[2] = 0;
            Xlength[3] = 0;
            Ylength[0] = 0;
            Ylength[1] = 0;
            Ylength[2] = 0;
            Ylength[3] = 0;
            Zlength[0] = 0;
            Zlength[1] = 0;
            Zlength[2] = 0;
            Zlength[3] = 0;
            timelength = 0;
            Xactive = 0;
            Yactive = 0;
            Zactive = 0;
            step = 0;
            Xstep = 0;
            Ystep = 0;
            Zstep = 0;
            steptime = 0;
            Deactivate = true;
            UpdateEvents(Animated);
            Animating();
        }

        [KSPEvent(guiActiveEditor = true, guiActive = true, guiName = Stopping, active = false, groupName = groupName, groupDisplayName = groupName)]
        protected void Stop()
        {
            if (node != null)
            {
                foreach (AttachNode unit in node)
                {
                    if (unit.id == "top")
                    {
                        if (unit.attachedPart != null)
                        {
                            attachedposition = unit.attachedPart.transform.position;
                            unit.attachedPart.transform.position = attachedposition + unit.originalPosition - unit.position;
                        }
                        unit.position = unit.originalPosition;
                        currentposition = unit.position;
                    }
                }
            }
            Animated = false;
            UpdateEvents(Animated);
        }

        private void UpdateEvents(bool Animated)
        {
            Events["Animate"].active = !Animated;
            Events["Stop"].active = Animated;
            if (Deactivate)
            {
                Events["Animate"].active = false;
                Events["Stop"].active = false;

            }
        }


        protected void Automating()
        {
            if (node != null)
            {
                if (i < steptime)
                {
                    foreach (AttachNode unit in node)
                    {
                        if (unit.id == "top")
                        {
                            dp = new Vector3((Xstep * Xactive) / steptime, (Ystep * Yactive) / steptime, (Zstep * Zactive) / steptime);
                            unit.position = currentposition + dp;
                            currentposition = currentposition + dp;
                            if (unit.attachedPart != null)
                            {
                                attachedposition = unit.attachedPart.transform.position;
                                unit.attachedPart.transform.position = attachedposition + dp;
                            }
                        }
                    }
                    i = i + 1;
                }
                if (i >= steptime)
                {
                    if (!StrToInt())
                    {
                        Deactivate = false;
                        Animated = true;
                        UpdateEvents(Animated);
                        CancelInvoke("Automating");
                    }
                }
            }
        }

        void Animating()
        {
            if (StrToInt())
            {
                InvokeRepeating("Automating", 0, 0.1F);
            }
        }

        bool StrToInt()
        {
            if (timelength <= time.Length)
            {
                Xactive = 0;
                Yactive = 0;
                Zactive = 0;
                steptime = 0;
                step = step + 1;
                while (timelength < time.Length && time[timelength] != ' ')
                {
                    steptime = steptime * 10 + Int32.Parse(time[timelength].ToString());
                    timelength = timelength + 1;
                }
                timelength = timelength + 1;

                if (Xlength[3] < step)
                {
                    Xstep = 0;
                    Xlength[3] = 0;
                    Negative = false;
                    Float = false;
                    while (Xlength[2] < Xtransform.Length && Xtransform[Xlength[2]] != ' ')
                    {
                        Xlength[3] = Xlength[3] * 10 + Int32.Parse(Xtransform[Xlength[1]].ToString());
                        Xlength[2] = Xlength[2] + 1;
                    }
                    Xlength[2] = Xlength[2] + 1;
                    while (Xlength[2] < Xtransform.Length && Xtransform[Xlength[2]] != ' ')
                    {
                        if (Xtransform[Xlength[2]].ToString() == "-")
                        {
                            Negative = true;
                        }
                        else if (Xtransform[Xlength[2]].ToString() == "." || Xtransform[Xlength[2]].ToString() == ",")
                        {
                            Float = true;
                            i = 1;
                        }
                        else
                        {
                            if (Float == true)
                            {
                                Xstep = Xstep + ((Negative ? -1 : 1) * Int32.Parse(Xtransform[Xlength[2]].ToString()) * (float)Math.Pow(10, (-i)));
                            }
                            if (Float == false)
                            {
                                Xstep = Xstep * 10 + (Negative ? -1 : 1) * Int32.Parse(Xtransform[Xlength[2]].ToString());
                            }
                            i = i + 1;
                        }
                        Xlength[2] = Xlength[2] + 1;
                    }
                    Xlength[2] = Xlength[2] + 1;
                }
                if (Xlength[3] == step)
                {
                    Xactive = 1;
                }

                if (Ylength[3] < step)
                {
                    Ystep = 0;
                    Ylength[3] = 0;
                    Negative = false;
                    Float = false;
                    while (Ylength[2] < Ytransform.Length && Ytransform[Ylength[2]] != ' ')
                    {
                        Ylength[3] = Ylength[3] * 10 + Int32.Parse(Ytransform[Ylength[2]].ToString());
                        Ylength[2] = Ylength[2] + 1;
                    }
                    Ylength[2] = Ylength[2] + 1;
                    while (Ylength[2] < Ytransform.Length && Ytransform[Ylength[2]] != ' ')
                    {
                        if (Ytransform[Ylength[2]].ToString() == "-")
                        {
                            Negative = true;
                        }
                        else if (Ytransform[Ylength[2]].ToString() == "." || Ytransform[Ylength[2]].ToString() == ",")
                        {
                            Float = true;
                            i = 1;
                        }
                        else
                        {
                            if (Float == true)
                            {
                                Ystep = Ystep + ((Negative ? -1 : 1) * Int32.Parse(Ytransform[Ylength[2]].ToString()) * (float)Math.Pow(10, (-i)));
                            }
                            if (Float == false)
                            {
                                Ystep = Ystep * 10 + (Negative ? -1 : 1) * Int32.Parse(Ytransform[Ylength[2]].ToString());
                            }
                            i = i + 1;
                        }
                        Ylength[2] = Ylength[2] + 1;
                    }
                    Ylength[2] = Ylength[2] + 1;
                }
                if (Ylength[3] == step)
                {
                    Yactive = 1;
                }

                if (Zlength[3] < step)
                {
                    Zstep = 0;
                    Zlength[3] = 0;
                    Negative = false;
                    Float = false;
                    while (Zlength[2] < Ztransform.Length && Ztransform[Zlength[2]] != ' ')
                    {
                        Zlength[3] = Zlength[3] * 10 + Int32.Parse(Ztransform[Zlength[2]].ToString());
                        Zlength[2] = Zlength[2] + 1;
                    }
                    Zlength[2] = Zlength[2] + 1;
                    while (Zlength[2] < Ztransform.Length && Ztransform[Zlength[2]] != ' ')
                    {
                        if (Ztransform[Zlength[2]].ToString() == "-")
                        {
                            Negative = true;
                        }
                        else if (Ztransform[Zlength[2]].ToString() == "." || Ztransform[Zlength[2]].ToString() == ",")
                        {
                            Float = true;
                            i = 1;
                        }
                        else
                        {
                            if (Float == true)
                            {
                                Zstep = Zstep + ((Negative ? -1 : 1) * Int32.Parse(Ztransform[Zlength[2]].ToString()) * (float)Math.Pow(10, (-i)));
                            }
                            if (Float == false)
                            {
                                Zstep = Zstep * 10 + (Negative ? -1 : 1) * Int32.Parse(Ztransform[Zlength[2]].ToString());
                            }
                            i = i + 1;
                        }
                        Zlength[2] = Zlength[2] + 1;
                    }
                    Zlength[2] = Zlength[2] + 1;
                }
                if (Zlength[3] == step)
                {
                    Zactive = 1;
                }
                i = 0;

                return true;
            }
            else
            {
                return false;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            node = part.attachNodes;
            Animated = false;
            Deactivate = false;
            if (node != null)
            {
                foreach (AttachNode unit in node)
                {
                    if (unit.id == "top")
                    {
                        currentposition = unit.originalPosition;
                    }
                }
            }
            UpdateEvents(Animated);
        }
    }
}
