using PEKKAUtils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModulePekkaPEZ
{
    public class ModulePekkaPEZ : PartModule
    {
        public const string groupName = "StarController";

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string AnimationName = "Animation";

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 DecoupleForce = Vector3.zero;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public Vector3 Move = Vector3.zero;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public int maxIndex = 43;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float firstDecouple = 0f;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool Double = true;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float secondDecouple = 0.1f;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float nodeMove = 1.8f;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float moveTime = 10f;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public float decoupleTime = 15f;

        float curTime = 0f;

        int curindex = -1;
        int maxTime = 0;
        int maxAttached = 0;

        bool nodes = false;
        bool Decouple = false;
        bool firstDecoupled = false;
        bool secondDecoupled = false;
        bool passed = false;

        Vector3 dp = Vector3.zero;
        Vector3 nodePosition = Vector3.zero;
        //Vector3 Move = new Vector3(0, -0.27f, 0);//new Vector3(10, 0, 0);//new Vector3(0f, -0.27f, 0f);
        Vector3 attachedPosition = Vector3.zero;

        Part DecoupledPart;

        List<string> MoveNodes = new List<string>();
        List<Vector3> DecoupledPos = new List<Vector3>();
        List<Part> DecoupledParts = new List<Part>();

        List<AttachNode> node;

        bool isAnimating = false;
        public Animation Animation;
        public float AnimationTime = 0f;
        public float AnimationTimeWanted = 0f;
        public float oldAnimationSpeed = 0f;
        public int currentstate = 1;

        RenderLine deployLine;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Decouple Starlinks", active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void StarDecouple()
        {
            curTime = 0;

            if (Double == false)
            {
                maxTime = (maxIndex + 1);
            }
            else
            {
                maxTime = ((maxIndex + 3) / 2);
            }

            AnimationTimeWanted = (float)(Animation[AnimationName].length * (int)(1 + (maxAttached+1) / 2) / (int)(1 + (maxIndex+1) / 2));
            StartAnimation();
            Decouple = true;
            Events["StarDecouple"].active = false;
        }

        void AnimationStart()
        {
            if (Math.Abs(AnimationTime - AnimationTimeWanted) > 0.1)
            {
                if (AnimationTimeWanted > AnimationTime)
                {
                    Animation[AnimationName].speed = Animation[AnimationName].length / ((maxIndex + 3) * moveTime / 2);
                    oldAnimationSpeed = 1;
                }
                else if (AnimationTimeWanted < AnimationTime)
                {
                    Animation[AnimationName].speed = -Animation[AnimationName].length / ((maxIndex + 3) * moveTime / 2);
                    oldAnimationSpeed = -1;
                }
                Animation[AnimationName].time = AnimationTime;
                Animation[AnimationName].enabled = true;
                Animation.Play(AnimationName);
                isAnimating = true;
            }
        }

        void AnimationStop()
        {
            if ((Animation[AnimationName].time >= AnimationTimeWanted && oldAnimationSpeed > 0) || (Animation[AnimationName].time <= AnimationTimeWanted && oldAnimationSpeed < 0))
            {
                AnimationTime = Animation[AnimationName].time;
                Animation.Stop(AnimationName);
                isAnimating = false;
            }
            if (AnimationTimeWanted >= Animation[AnimationName].length && Animation[AnimationName].enabled == false)
            {
                AnimationTime = Animation[AnimationName].length;
                isAnimating = false;
            }
        }

        public void StartAnimation()
        {
            Animation[AnimationName].speed = Animation[AnimationName].length / ((maxIndex + 3) * moveTime / 2);
            Animation[AnimationName].time = AnimationTimeWanted;
            Animation[AnimationName].enabled = true;
            Animation.Play(AnimationName);
            isAnimating = true;
            oldAnimationSpeed = 1;

        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                AnimationTimeWanted = (float)(Animation[AnimationName].length * ((int)(1 + (maxAttached + 1) / 2) - Math.Ceiling(curTime / decoupleTime)) / (int)(1 + (maxIndex + 1) / 2));
                if (currentstate == 1)
                {
                    StartAnimation();
                    currentstate = 0;
                }
                else if (isAnimating == true)
                {
                    AnimationStop();
                }
            }

            foreach (AttachNode unit in node)
            {
                if (unit.id == "starlink-00")
                {
                    deployLine.Update(transform.position + transform.TransformDirection(unit.position), transform.TransformDirection(DecoupleForce.normalized * 10));
                }
            }

            if (Decouple == true)
            {
                if (curTime % decoupleTime > firstDecouple && firstDecoupled == false)
                {
                    double curTimeInt = Math.Floor(curTime / decoupleTime)*2;
                    string searchnode = "";

                    if (curTimeInt < 10)
                    {
                        searchnode = "starlink-0" + curTimeInt.ToString();
                    }
                    else
                    {
                        searchnode = "starlink-" + curTimeInt.ToString();
                    }

                    if (node != null)
                    {
                        foreach (AttachNode unit in node)
                        {
                            if (unit.id == searchnode)
                            {
                                if (unit.attachedPart != null)
                                {
                                    DecoupledPart = unit.attachedPart;
                                    DecoupledParts.Add(DecoupledPart);
                                    DecoupledPart.decouple();
                                    DecoupledPart.AddForce(transform.TransformDirection(DecoupleForce));// + Move*DecoupledPart.mass));
                                    node = part.attachNodes;
                                }
                                firstDecoupled = true;
                                node.Remove(unit);
                                passed = false;
                                break;
                            }
                        }
                    }
                }
                if (curTime % decoupleTime > secondDecouple && secondDecoupled == false)
                {
                    double curTimeInt = Math.Floor(curTime / decoupleTime)*2+1;
                    string searchnode = "";

                    if (curTimeInt < 10)
                    {
                        searchnode = "starlink-0" + curTimeInt.ToString();
                    }
                    else
                    {
                        searchnode = "starlink-" + curTimeInt.ToString();
                    }

                    if (node != null)
                    {
                        foreach (AttachNode unit in node)
                        {
                            if (unit.id == searchnode)
                            {
                                if (unit.attachedPart != null)
                                {
                                    DecoupledPart = unit.attachedPart;
                                    DecoupledParts.Add(DecoupledPart);
                                    DecoupledPart.decouple();
                                    DecoupledPart.AddForce(transform.TransformDirection(DecoupleForce));// + Move*DecoupledPart.mass));
                                    node = part.attachNodes;
                                }
                                curindex += 1;
                                secondDecoupled = true;
                                node.Remove(unit);
                                passed = false;
                                break;
                            }
                        }
                    }
                }
                if (node != null && curTime % decoupleTime > nodeMove && curTime % decoupleTime < nodeMove + moveTime)
                {
                    if (isAnimating == false)
                    {
                        AnimationStart();
                    }
                    foreach (AttachNode unit in node)
                    {
                        if (MoveNodes.Contains(unit.id))
                        {
                            if (unit.attachedPart != null)
                            {
                                //          works everytime but its ugly
                                unit.attachedPart.transform.GetChild(0).position += transform.TransformDirection(Move) * TimeWarp.fixedDeltaTime / moveTime;


                                //unit.attachedPart.attachNodes[0].position = nodePosition - Move * TimeWarp.fixedDeltaTime / moveTime;
                                //nodePosition = nodePosition - Move * TimeWarp.fixedDeltaTime / moveTime;

                                //unit.attachedPart.attachNodes[0].position -= Move * TimeWarp.fixedDeltaTime / moveTime;
                                //PartJoint jointNode = unit.attachedPart.attachJoint;

                                //if (!passed)
                                //{
                                //    foreach (ConfigurableJoint joint in unit.attachedPart.transform.GetComponentsInChildren<ConfigurableJoint>().ToList())
                                //    {
                                //        if (jointNode.Joint != joint)
                                //        {
                                //            Destroy(joint);
                                //        }
                                //    }
                                //
                                //    //          works sometimes
                                //    //jointNode.Joint.yMotion = ConfigurableJointMotion.Free;
                                //    //jointNode.Joint.targetAngularVelocity = Vector3.zero;
                                //    
                                //    //JointDrive driver = jointNode.Joint.yDrive;
                                //    //driver.positionSpring = 100;
                                //    //driver.positionDamper = 100;
                                //    //driver.maximumForce = 100;
                                //
                                //    //          works sometimes
                                //    //jointNode.Joint.xMotion = ConfigurableJointMotion.Free;
                                //    //jointNode.Joint.targetAngularVelocity = Vector3.zero;
                                //    //
                                //    //JointDrive driver = jointNode.Joint.xDrive;
                                //    //driver.positionSpring = 100;
                                //    //driver.positionDamper = 100;
                                //    //driver.maximumForce = 100;
                                //}

                                //JointDrive driver = jointNode.Joint.yDrive;
                                //driver.positionSpring = 100;
                                //driver.positionDamper = 100;
                                //driver.maximumForce = 100;
                                //jointNode.Joint.anchor -= Move * TimeWarp.fixedDeltaTime / moveTime;
                            }
                            else
                            {
                                node.Remove(unit);
                            }
                        }
                    }
                    passed = true;
                }
                //foreach (Part Part in DecoupledParts)
                //{
                //    Collider[] ColliderList = Part.GetPartColliders();
                //    foreach (Collider collider in ColliderList)
                //    {
                //        collider.enabled = false;
                //    }
                //    Part.vessel.angularVelocity = Vector3.zero;
                //}
                curTime += TimeWarp.fixedDeltaTime;
                if (curTime / decoupleTime > curindex)
                {
                    firstDecoupled = false;
                    secondDecoupled = false;
                }
                if (Double == false && (curTime / decoupleTime) > (int)(1 + maxAttached/2))
                {
                    Events["StarDecouple"].active = true;
                    Decouple = false;
                }
                else if (Double && (curTime / decoupleTime) > (int)(1 + maxAttached / 2))
                {
                    Events["StarDecouple"].active = true;
                    Decouple = false;
                }
            }
        }

        public override void OnLoad(ConfigNode TestNode)
        {
            currentstate = 1;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            node = part.attachNodes;
            int i = 0;

            while (i <= maxIndex)
            {
                if (i < 10)
                {
                    MoveNodes.Add("starlink-0" + i.ToString());
                    DecoupledPos.Add(Vector3.zero);
                }
                else
                {
                    MoveNodes.Add("starlink-" + i.ToString());
                    DecoupledPos.Add(Vector3.zero);
                }
                i = i + 1;
            }
            foreach (AttachNode unit in node)
            {
                if (MoveNodes.Contains(unit.id))
                {
                    if (unit.attachedPart != null)
                    {
                        DecoupledPos[MoveNodes.IndexOf(unit.id)] = unit.attachedPart.transform.GetChild(0).position;
                        if (MoveNodes.IndexOf(unit.id) > maxAttached)
                        {
                            maxAttached = MoveNodes.IndexOf(unit.id);
                        }
                    }
                }
            }

            deployLine = new RenderLine(part, "deployDir", Color.white);
            deployLine.setActive(state == StartState.Editor);

            Animation = part.FindModelAnimator(AnimationName);
            AnimationTime = Animation[AnimationName].length;
            AnimationTimeWanted = Animation[AnimationName].length;

            Move *= part.scaleFactor;

            nodes = true;
            Decouple = false;
        }
    }
}
