
#undef Timed

using PEKKAUtils;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Targeting;

namespace ModulePekkaVent
{
    public class Controller
    {
        const string TAG = "[ModulePekkaVent]";
        public bool isActive = true;
        public bool useAtm = false;
        public Curve atmCurve = null;
        public int atmStartCfg = 0;
        public int atmEndCfg = 1;


        public bool useThrust = false;
        public Transform collectorTransform;
        public Curve thrustCurve = null;
        public double startThreshold = 0;
        public double endThreshold = 100;
        public double thrustPower = 1;
        public double distancePower = 1;
        public double endPower = 1;
        public float collectorRadius = 0;
        public int thrustStartCfg = 0;
        public int thrustEndCfg = 1;

        public bool engineController = false;
        public bool startupEffect = false;
        public float startupTime = 0;
        public bool shutdownEffect = false;
        public float shutdownTime = 0;
        public float oldThrottle = 0;
        public double startTime = 0;

        public ModuleEnginesFX engine = null;


        public bool useSpeed = false;
        public bool speedIsForce = false;
        public double speedPower = 1;
        public double speedMultiplier = 1;
        public double atmPower = 1;


        public bool useWind = false;
        public bool windIsForce = false;
        public float maxWind = 1;


        public Transform endTransform = null;
        public string orientation = "forward";
        public string axis = "x";

        public bool hideEffects = false;
        public float hideSize = 1;

        public Vector3d GetWind(double windPower, double windDir, Vessel vessel)
        {
            Vector3d windStrength = Vector3d.zero;
            if (useWind)
            {
                // A global wind vector is calculated here and does not have any unit as wind power... i could not be bothered
                // I've heard that this is also somewhat done in FAR if someone wants to integrate that please do so
                windStrength = maxWind * windPower * Math.Cos(windDir * Math.PI / 180) * vessel.north;
                windStrength += maxWind * windPower * Math.Sin(windDir * Math.PI / 180) * vessel.east;
            }
            return windStrength;
        }

        public double quadraticFormula(double a, double b, double c, bool debug)
        {
            this.Log(debug, TAG + "a: " + a);
            this.Log(debug, TAG + "b: " + b);
            this.Log(debug, TAG + "c: " + c);

            if (a == 0)
            {
                return -c / b;
            }
            // I would like to use some integrated function as it would be way better but haven't found it so far
            double sqrt = Math.Pow(b, 2) - 4 * a * c;
            if (sqrt > 0)
            {
                double result1 = (-b + Math.Sqrt(sqrt)) / (2 * a);
                double result2 = (-b - Math.Sqrt(sqrt)) / (2 * a);
                // There may be a more elegant way to return the result but at the moment it just returns the lowest result above 0
                if ((result1 < result2 || result2 < 0) && result1 > 0)
                    return result1;
                else if (result2 > 0)
                    return result2;
            }
            return 0;
        }

        public double GetMaxEnergy(KSPParticleEmitter Emitter, bool debug, Vector3 controllerVelocity, Vector3 controllerForce, double maxEnergy)
        {
            // Calculates the energy (lifetime) of the particles in order for them to end up at a plane at the endTransform which is aligned with the x axis of the emitter
            // I would like to also consider random velocity and force but cant be bothered right now
            // I would also like to consider all axis and have the end plane be aligned with the endTransform direction but thats too much maths for now
            double distance = 0;
            double energy = 0;
            if (orientation == "forward")
                distance = -Vector3.Dot(endTransform.position - Emitter.transform.position, Emitter.transform.forward);
            else if (orientation == "right")
                distance = -Vector3.Dot(endTransform.position - Emitter.transform.position, Emitter.transform.right);
            else
                distance = -Vector3.Dot(endTransform.position - Emitter.transform.position, Emitter.transform.up);

            if (axis == "x")
                energy = quadraticFormula(Emitter.force.x + Emitter.transform.InverseTransformDirection(controllerForce).x, Emitter.localVelocity.x + Emitter.transform.InverseTransformDirection(Emitter.worldVelocity + controllerVelocity).x, distance, debug);
            else if (axis == "y")
                energy = quadraticFormula(Emitter.force.y + Emitter.transform.InverseTransformDirection(controllerForce).y, Emitter.localVelocity.y + Emitter.transform.InverseTransformDirection(Emitter.worldVelocity + controllerVelocity).y, distance, debug);
            else
                energy = quadraticFormula(Emitter.force.z + Emitter.transform.InverseTransformDirection(controllerForce).z, Emitter.localVelocity.z + Emitter.transform.InverseTransformDirection(Emitter.worldVelocity + controllerVelocity).z, distance, debug);

            this.Log(debug, "distance forward: " + Vector3.Dot(endTransform.position - Emitter.transform.position, Emitter.transform.forward));
            this.Log(debug, "distance right: " + Vector3.Dot(endTransform.position - Emitter.transform.position, Emitter.transform.right));
            this.Log(debug, "distance up: " + Vector3.Dot(endTransform.position - Emitter.transform.position, Emitter.transform.up));
            this.Log(debug, "energy: " + energy);

            if (energy <= 0 || maxEnergy < energy)
                return maxEnergy;
            else
                return energy;
        }

        public Vector3d GetSpeedStrength(Vector3 speed, double atm, double mach, bool debug)
        {
            Vector3d speedStrength = Vector3d.zero;
            speed = speed.normalized;
            if (useSpeed)
            {
                speedStrength.x = -speedMultiplier * ((speed.x >= 0) ? Math.Pow(speed.x, speedPower) : -Math.Pow(-speed.x, speedPower)) * Math.Pow(atm, atmPower) * mach;
                speedStrength.y = -speedMultiplier * ((speed.y >= 0) ? Math.Pow(speed.y, speedPower) : -Math.Pow(-speed.y, speedPower)) * Math.Pow(atm, atmPower) * mach;
                speedStrength.z = -speedMultiplier * ((speed.z >= 0) ? Math.Pow(speed.z, speedPower) : -Math.Pow(-speed.z, speedPower)) * Math.Pow(atm, atmPower) * mach;
            }
            this.Log(debug, "SpeedForce: " + speedStrength);

            return speedStrength;
        }

        public double GetThrustStrength(float slider, bool debug)
        {
            double thrustStrength = slider;
            double totalThrust = slowCheck(debug);
            // The next part seems way too complicated to just clamp the value between 0 and 1 i might want to make an extra function for that if it doesn't exist already...
            // Also the whole start and end value should be in an extra function probably... which i'm gonna design later
            if (thrustCurve != null)
                thrustStrength = thrustCurve.GetValue((float)totalThrust);
            else
                thrustStrength = Math.Min(Math.Max((totalThrust - startThreshold) / (endThreshold - startThreshold), 0), 1);

            this.Log(debug, "TotalThrust: " + totalThrust + " " + thrustStrength);

            return thrustStrength;
        }

        public double GetAtmStrength(float slider, double atm, bool debug)
        {
            double atmStrength = slider;
            // The next part seems way too complicated to just clamp the value between 0 and 1 i might want to make an extra function for that if it doesn't exist already...
            // Also the whole start and end value should be in an extra function probably... which i'm gonna design later
            if (atmCurve != null)
                atmStrength = atmCurve.GetValue((float)atm);

            this.Log(debug, "atmStrength: " + atmStrength);

            return atmStrength;
        }

        public double slowCheck(bool debug)
        {
            double power = 0;
            int engines = 0;

            if (engineController)
            {
                return engine.finalThrust;
            }

            // The tower of infinite complexity
            // Just searches every loaded vessel for engines and looks if they would hit the collector cicle... (i don't know if i should look if they are active first)
            // ... adds the engine thrust divided by its distance to the total and counts the number of engine tarnsforms effecting it
            foreach (Vessel ship in FlightGlobals.VesselsLoaded)
            {
                foreach (Part engine in ship.Parts)
                {
                    if (engine.FindModuleImplementing<ModuleEnginesFX>())
                    {
                        foreach (ModuleEnginesFX engineModule in engine.FindModulesImplementing<ModuleEnginesFX>())
                        {
                            if (engineModule.finalThrust != 0)
                            {
                                foreach (Transform thrustTransform in engineModule.thrustTransforms)
                                {
                                    double distance = Vector3.Dot(thrustTransform.position - collectorTransform.position, thrustTransform.forward);
                                    if (Math.Sqrt(Math.Pow((thrustTransform.position - collectorTransform.position).magnitude, 2) - Math.Pow(distance, 2)) <= collectorRadius)
                                    {
                                        power += Math.Pow(engineModule.finalThrust / engineModule.thrustTransforms.Count, thrustPower) / Math.Pow(Math.Abs(distance), distancePower);
                                        engines += 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            double result = Math.Pow(power, endPower);

            this.Log(debug, " EnginePower " + result + "EnigneTransforms: " + engines);

            return result;
        }
    }

    public class TimedAction
    {
        public bool toggleActive = false;
        public int moveToCfg = 0;
        public float startTime = 0;
        public float endTime = 0;
    }

    public class Config
    {
        public int cfgNumber = 0;
        public bool changeAtStart = false;
        public bool useWorldSpace = false;
        public float shape1D = 0;
        public float minSize = 0;
        public float maxSize = 0;
        public float maxParticleSize = 0;
        public float minEnergy = 0;
        public float maxEnergy = 0;
        public int minEmission = 0;
        public int maxEmission = 0;
        public Vector3 worldVelocity = Vector3.zero;
        public Vector3 velocity = Vector3.zero;
        public Vector3 rndVelocity = Vector3.zero;
        public float sizeGrow = 0;
        public Vector3 worldForce = Vector3.zero;
        public Vector3 force = Vector3.zero;
        public Vector3 rndForce = Vector3.zero;
        public double changeTime = 0;

        public void CreateConfig(KSPParticleEmitter emitter)
        {
            useWorldSpace = emitter.useWorldSpace;
            shape1D = emitter.shape1D;
            minSize = emitter.minSize;
            maxSize = emitter.maxSize;
            maxParticleSize = emitter.maxParticleSize;
            minEnergy = emitter.minEnergy;
            maxEnergy = emitter.maxEnergy;
            minEmission = emitter.minEmission;
            maxEmission = emitter.maxEmission;
            worldVelocity = emitter.worldVelocity;
            velocity = emitter.localVelocity;
            rndVelocity = emitter.rndVelocity;
            sizeGrow = emitter.sizeGrow;
            force = emitter.force;
            rndForce = emitter.rndForce;
        }

        public Config(Config copy)
        {
            cfgNumber = copy.cfgNumber;
            changeAtStart = copy.changeAtStart;
            useWorldSpace = copy.useWorldSpace;
            shape1D = copy.shape1D;
            minSize = copy.minSize;
            maxSize = copy.maxSize;
            maxParticleSize = copy.maxParticleSize;
            minEnergy = copy.minEnergy;
            maxEnergy = copy.maxEnergy;
            minEmission = copy.minEmission;
            maxEmission = copy.maxEmission;
            worldVelocity = copy.worldVelocity;
            velocity = copy.velocity;
            rndVelocity = copy.rndVelocity;
            sizeGrow = copy.sizeGrow;
            worldForce = copy.worldForce;
            force = copy.force;
            rndForce = copy.rndForce;
            changeTime = copy.changeTime;
        }
        public Config()
        { }
    }

    public class ParticleFX
    {
        const string TAG = "[StarshipVent]";
        public List<KSPParticleEmitter> particle = new List<KSPParticleEmitter>();

        public int particles = 0;
        
        public bool visible = false;
        public bool allVisible = false;
        public bool allInvisible = false;
        public bool changedVisible = false;

        public int activeConfig = 0;
        public List<Config> configs = new List<Config>();
        public Config oldConfig = new Config();
        public Config newConfig = new Config();

        public int activeAction = -1;
        public List<TimedAction> timedActions = new List<TimedAction>();

        public Controller controller = null;

        public double startTime = 0;
        public double oldStartTime = double.NegativeInfinity;
        public double changeTime = 1;

        public double fadeInTime = 0;
        public double fadeOutTime = 0;

        public float Difference = 0f;

        public Vector3 controllerVelocity = Vector3.zero;
        public Vector3 controllerForce = Vector3.zero;

        public Vector3 oldControllerForce = Vector3.zero;
        public Vector3 oldForce = Vector3.zero;
        public Vector3 oldWorldForce = Vector3.zero;
        public Quaternion oldRot = Quaternion.identity;

        public Config startConfig = new Config();


        public bool ControlEffect(float slider, double wind, double dir, Vessel vessel, bool debug = false)
        {
            bool hideevent = false;

            this.Log(debug, "Controller maxEmission: " + particles);

            if (controller.hideEffects && visible == true)
            {
                CheckParticles(vessel);
            }

            if (allInvisible == false)
            {
                if (controller.engineController)
                {
                    if (controller.startupEffect)
                    {
                        if (debug && controller.startupTime > 0 && visible)
                        {
                            this.Log(debug, (controller.startTime + controller.startupTime - Planetarium.GetUniversalTime()).ToString());
                        }
                        if (controller.oldThrottle < controller.engine.currentThrottle && controller.oldThrottle != 0 && controller.engine.currentThrottle < controller.engine.minThrust / controller.engine.maxThrust && visible == false)
                        {
                            if (controller.startupTime > 0)
                                controller.startTime = Planetarium.GetUniversalTime();
                            ShowParticleEvent(wind, dir, vessel, debug);
                        }
                        else if (visible && ((controller.startupTime > 0 && controller.startTime + controller.startupTime < Planetarium.GetUniversalTime()) || (controller.startupTime == 0 && controller.engine.currentThrottle >= controller.engine.minThrust / controller.engine.maxThrust)))
                            hideevent = HideParticleEvent(slider, wind, dir, true, vessel, debug);
                    }
                    if (controller.shutdownEffect)
                    {
                        if (debug && controller.shutdownTime > 0 && visible)
                        {
                            this.Log(debug, (controller.startTime + controller.shutdownTime - Planetarium.GetUniversalTime()).ToString());
                        }
                        if (controller.oldThrottle > controller.engine.currentThrottle && controller.oldThrottle != 0 && controller.engine.currentThrottle < controller.engine.minThrust / controller.engine.maxThrust && visible == false)
                        {
                            if (controller.shutdownTime > 0)
                                controller.startTime = Planetarium.GetUniversalTime();
                            ShowParticleEvent(wind, dir, vessel, debug);
                        }
                        else if (visible && ((controller.shutdownTime > 0 && controller.startTime + controller.shutdownTime < Planetarium.GetUniversalTime()) || (controller.shutdownTime == 0 && controller.oldThrottle == 0)))
                            hideevent = HideParticleEvent(slider, wind, dir, true, vessel, debug);
                    }
                    controller.oldThrottle = controller.engine.currentThrottle;
                }

                // lets controller generate a config to be used which is abusing the config system a bit... but works i guess ¯\_(-.-)_/¯
                newConfig = ControllerConfig(slider, wind, dir, newConfig, vessel, debug);

                ChangeEffect(vessel, 0, true, true);

                if (controller.endTransform != null)
                {
                    foreach (KSPParticleEmitter Emitter in particle)
                    {
                        Emitter.maxEnergy = (float)controller.GetMaxEnergy(Emitter, debug, controllerVelocity, controllerForce, newConfig.maxEnergy);

                        this.Log(debug, "max Energy: " + Emitter.maxEnergy);

                        if (Emitter.maxEnergy < Emitter.minEnergy)
                            Emitter.minEnergy = Emitter.maxEnergy;
                    }
                }
            }
            return hideevent;
        }

        public Config ControllerConfig(float slider, double wind, double dir, Config oldconfig, Vessel vessel, bool debug = false)
        {
            controllerVelocity = Vector3.zero;
            controllerForce = Vector3.zero;
            Config config = new Config(oldconfig);
            Config thrustConfig = null;
            Config atmConfig = null;

            if (controller.useThrust || (controller.engineController && controller.startupEffect == false && controller.shutdownEffect == false))
                thrustConfig = Interpolate(configs[controller.thrustStartCfg], configs[controller.thrustEndCfg], (float)controller.GetThrustStrength(slider, debug));
            if (controller.useAtm)
                atmConfig = Interpolate(configs[controller.atmStartCfg], configs[controller.atmEndCfg], (float)controller.GetAtmStrength(slider, vessel.atmDensity, debug));

            if (thrustConfig != null && atmConfig != null)
                config = Interpolate(thrustConfig, atmConfig, 0.5f);
            else if (thrustConfig != null)
                config = thrustConfig;
            else if (atmConfig != null)
                config = atmConfig;

            if (controller.windIsForce)
                controllerForce += controller.GetWind(wind, dir, vessel);
            else
                controllerVelocity += controller.GetWind(wind, dir, vessel);
            
            if (controller.speedIsForce)
                controllerForce += controller.GetSpeedStrength(vessel.srf_velocity, vessel.atmDensity, vessel.mach, debug);
            else
                controllerVelocity += controller.GetSpeedStrength(vessel.srf_velocity, vessel.atmDensity, vessel.mach, debug);

            controllerVelocity = (UnityEngine.Random.Range(-1,1) > 0) ? controllerVelocity : -controllerVelocity;
            return config;
        }

        public bool NextActionEvent(float slider, double effectsTime, bool visible, double wind, double dir, Vessel vessel, bool debug)
        {
            bool changeevent = false;

            activeAction += 1;

            this.Log(debug, "next action " + activeAction);

            if (timedActions[activeAction].toggleActive)
            {
                if (visible)
                    changeevent = HideParticleEvent(slider, wind, dir, false, vessel, false, timedActions[activeAction].startTime != timedActions[activeAction].endTime, debug, (timedActions[activeAction].endTime == 0) ? 0 : timedActions[activeAction].endTime - timedActions[activeAction].startTime);
                else
                    ShowParticleEvent(wind, dir, vessel, false, timedActions[activeAction].startTime != timedActions[activeAction].endTime, debug, (timedActions[activeAction].endTime == 0) ? 0 : timedActions[activeAction].endTime - timedActions[activeAction].startTime);
            }

            else
            {
                startTime = effectsTime + timedActions[activeAction].startTime;
                oldConfig = new Config(newConfig);
                activeConfig = timedActions[activeAction].moveToCfg;
                newConfig = new Config(configs[activeConfig]);

                if (timedActions[activeAction].endTime == 0)
                    changeTime = newConfig.changeTime;
                else
                    changeTime = timedActions[activeAction].endTime - timedActions[activeAction].startTime;
            }

            return changeevent;
        }

        public float Scalar()
        {
            // Useless function with weird name
            return Math.Max(Math.Min((float)((Planetarium.GetUniversalTime() - startTime) / changeTime), 1), 0);
        }

        public Config Interpolate(Config Config1, Config Config2, float Scalar)
        {
            // Optimizing cases where you don't have to interpolate at all
            if (Scalar == 1)
                return Config2;
            else if (Scalar == 0)
                return Config1;

            else
            {
                Config newCfg = new Config();
                if (newCfg.changeAtStart)
                    newCfg.useWorldSpace = Config2.useWorldSpace;
                else
                    newCfg.useWorldSpace = Config1.useWorldSpace;

                newCfg.shape1D = Config1.shape1D + (Config2.shape1D - Config1.shape1D) * Scalar;
                newCfg.minSize = Config1.minSize + (Config2.minSize - Config1.minSize) * Scalar;
                newCfg.maxSize = Config1.maxSize + (Config2.maxSize - Config1.maxSize) * Scalar;
                newCfg.maxParticleSize = Config1.maxParticleSize + (Config2.maxParticleSize - Config1.maxParticleSize) * Scalar;
                newCfg.minEnergy = Config1.minEnergy + (Config2.minEnergy - Config1.minEnergy) * Scalar;
                newCfg.maxEnergy = Config1.maxEnergy + (Config2.maxEnergy - Config1.maxEnergy) * Scalar;
                newCfg.minEmission = Config1.minEmission + (int)Math.Round((Config2.minEmission - Config1.minEmission) * Scalar);
                newCfg.maxEmission = Config1.maxEmission + (int)Math.Round((Config2.maxEmission - Config1.maxEmission) * Scalar);
                newCfg.worldVelocity = Config1.worldVelocity + (Config2.worldVelocity - Config1.worldVelocity) * Scalar;
                newCfg.velocity = Config1.velocity + (Config2.velocity - Config1.velocity) * Scalar;
                newCfg.rndVelocity = Config1.rndVelocity + (Config2.rndVelocity - Config1.rndVelocity) * Scalar;
                newCfg.sizeGrow = Config1.sizeGrow + (Config2.sizeGrow - Config1.sizeGrow) * Scalar;
                newCfg.worldForce = Config1.worldForce + (Config2.worldForce - Config1.worldForce) * Scalar;
                newCfg.force = Config1.force + (Config2.force - Config1.force) * Scalar;
                newCfg.rndForce = Config1.rndForce + (Config2.rndForce - Config1.rndForce) * Scalar;
                // In the case it's being used currently (undoing an unfinished transition) changeTime is more useful this way
                // although in other cases i can't think of it might be the other way around but i don't wanna add another random toggle
                // because i have done that more than enough
                newCfg.changeTime = Config1.changeTime * (1 - Scalar);  //newCfg.changeTime = Config2.changeTime * Scalar;
                return newCfg;
            }
        }

        public void CheckParticles(Vessel vessel)
        {
            allVisible = true;
            allInvisible = true;
            if (visible)
                foreach (KSPParticleEmitter emitter in particle)
                {
                    bool emit = emitter.emit;
                    float magnitude = emitter.transform.localScale.magnitude;
                    if (emit == false && magnitude >= controller.hideSize)
                    {
                        emitter.emit = true;
                        UpdateParticle(emitter, vessel, 1, true, true);

                        oldForce = newConfig.force;
                        oldWorldForce = newConfig.worldForce;
                        oldControllerForce = controllerForce;
                        oldRot = vessel.vesselTransform.rotation;
                    }
                    if (magnitude < controller.hideSize)
                    {
                        emitter.emit = false;
                        allVisible = false;
                    }
                    else
                    {
                        allInvisible = false;
                    }
                }
        }

        public void ShowParticleEvent(double wind, double dir, Vessel vessel, bool debug, bool atStart = false, bool fadein = false, double fadeintime = 0)
        {
            visible = true;
            // do not dynamically change the effect at start... i've had bad things happen then

            this.Log(debug, "showParticle " + particles);

            if (controller == null || atStart || fadein || fadeInTime != 0)
            {
                if (fadeInTime != 0 || fadein == true)
                {
                    FadeInEvent(fadeintime);
                }
                else
                {
                    oldConfig = new Config(newConfig);
                    newConfig = new Config(configs[activeConfig]);
                    ChangeEffect(vessel, 0, true, false);
                }
            }
            else
            {
                if (controller.useThrust && controller.useAtm == false)
                    oldConfig = configs[controller.thrustStartCfg];
                else if (controller.useThrust == false && controller.useAtm)
                    oldConfig = configs[controller.atmStartCfg];
                else
                    oldConfig = Interpolate(configs[controller.thrustStartCfg], configs[controller.atmStartCfg], 0.5f);
                ControlEffect(0, wind, dir, vessel);
            }

            foreach (KSPParticleEmitter emitter in particle)
            {
                emitter.emit = true;
                if (controller != null && controller.hideEffects && emitter.transform.localScale.magnitude > controller.hideSize)
                    emitter.emit = false;
            }

        }

        public bool HideParticleEvent(float slider, double wind, double dir, bool effectsChanging, Vessel vessel, bool debug, bool deactivateFadeout = false, bool fadeout = false, double fadeouttime = 0)
        {
            visible = false;

            this.Log(debug, "hideParticle " + particles);

            if (deactivateFadeout == false && (fadeOutTime != 0 || fadeout == true))
            {
                this.Log(debug, "fadeOut");

                if (effectsChanging)
                    FadeOutEvent(slider, wind, dir, vessel, fadeouttime, Interpolate(newConfig, oldConfig, Scalar()));
                else
                    FadeOutEvent(slider, wind, dir, vessel, fadeouttime, configs[activeConfig]);
            }
            else
                foreach (KSPParticleEmitter emitter in particle)
                    emitter.emit = false;
            // Returns true if particle has fadeOff active currently... Always has to return a flag for the program loop to know if particle is active even though it was commanded to shut off
            return (deactivateFadeout == false && (fadeOutTime != 0 || fadeout == true));
        }

        public void ResetConfigEvent(Vessel vessel)
        {
            // might wanna get this the function of setting any active config to replace nextconfig and previousconfig
            oldConfig = new Config(newConfig);
            activeConfig = 0;
            newConfig = new Config(configs[activeConfig]);
            ChangeEffect(vessel, 0, true);
        }

        public void FadeInEvent(double Time = 0)
        {
            startTime = Planetarium.GetUniversalTime();
            newConfig = new Config(configs[activeConfig]);
            oldConfig = new Config(newConfig);
            oldConfig.velocity = Vector3.zero;
            oldConfig.worldVelocity = Vector3.zero;
            oldConfig.sizeGrow = 0;
            if (Time == 0)
                changeTime = fadeOutTime;
            else
                changeTime = Time;
            return;
        }

        public void FadeOutEvent(float slider, double wind, double dir, Vessel vessel, double Time = 0, Config oldconfig = null)
        {
            if (controller == null)
                    oldConfig = new Config(oldconfig);
            else
                    oldConfig = ControllerConfig(slider, wind, dir, oldconfig, vessel);

            startTime = Planetarium.GetUniversalTime();
            newConfig = new Config(oldConfig);
            newConfig.velocity = Vector3.zero;
            newConfig.worldVelocity = Vector3.zero;
            newConfig.sizeGrow = 0;
            if (Time == 0)
                changeTime = fadeOutTime;
            else
                changeTime = Time;
            return;
        }

        public void NextConfigEvent()
        {
            startTime = Planetarium.GetUniversalTime();
            oldConfig = new Config(newConfig);
            activeConfig = activeConfig + 1;
            if (activeConfig == configs.Count)
                activeConfig = 0;

            newConfig = new Config(configs[activeConfig]);
            changeTime = newConfig.changeTime;
            return;
        }

        public void PreviousConfigEvent()
        {
            startTime = Planetarium.GetUniversalTime();
            oldConfig = new Config(newConfig);
            activeConfig = activeConfig - 1;
            if (activeConfig == -1)
                activeConfig = configs.Count - 1;

            newConfig = new Config(configs[activeConfig]);
            changeTime = newConfig.changeTime;
            return;
        }

        public int OrderConfigs(Config x, Config y)
        {
            // I really don't like these two functions to simply sort the configs
            if (x.cfgNumber > y.cfgNumber)
                return 1;
            else if (x.cfgNumber < y.cfgNumber)
                return -1;
            else
                return 0;
        }
        public void SortConfigs()
        {
            configs.Sort(OrderConfigs);
            return;
        }

        public int SortTimedActions(TimedAction x, TimedAction y)
        {
             // I also don't like this
            if (x.startTime > y.startTime)
                return 1;
            else if (x.startTime < y.startTime)
                return -1;
            else
                return 0;
        }
        public void SortActions()
        {
            timedActions.Sort(SortTimedActions);
            return;
        }

        public void ChangeEffect(Vessel vessel, double changetime = 0, bool instantchange = false, bool changeforce = false)
        {
            if (Planetarium.GetUniversalTime() - startTime < 0)
                return;

            // If changing isn't as easy as just changing the effect every frame...
            // Can we talk about how bad the KSPParticleEffects are...
            // I really like them don't get me wrong...
            // It's just that changing the force is something you don't ever want to do...
            // Why do you have to revert the Particle to the starting configs...
            // And that just to change the force... and then you can't even imedeatly change it back...
            // No you have to wait some time which i can't even figure out...
            // So if someone finds out how long you have to wait or can improve the wait time...
            // That would be great... ¯\_(ツ)_/¯
            // I am just fed up right now so i don't wanna figure out which time works best...
            // This f***inng thing took me two weeks to figure out to this extend...
            // I don't wanna talk about what this did to me

            if (oldStartTime != double.NegativeInfinity)
                startTime = oldStartTime;
            oldStartTime = double.NegativeInfinity;

            if (changetime != 0)
                changeTime = changetime;

            float timeNorm = Math.Max(Math.Min((float)((Planetarium.GetUniversalTime() - startTime) / changeTime), 1), 0);
            if (instantchange)
                timeNorm = 1;

            bool changedforce = false;

            foreach (KSPParticleEmitter unit in particle)
            {
                //vent.print(TAG + unit.transform.localPosition);
                changedforce = UpdateParticle(unit, vessel, timeNorm, changeforce);
            }
            if (changedforce)
            {
                // if we've updated the force in this update we also have to update the stored variables
                // this does not directly happen in the UpdtaeForce function because that function gets run for each effect
                // and then only the first effect would be updated while the rest would never get updated
                oldForce = oldConfig.force + (newConfig.force - oldConfig.force) * timeNorm;
                oldWorldForce = oldConfig.worldForce + (newConfig.worldForce - oldConfig.worldForce) * timeNorm;
                oldControllerForce = controllerForce;
                oldRot = vessel.vesselTransform.rotation;
            }
            return;
        }

        public bool UpdateParticle(KSPParticleEmitter unit, Vessel vessel, float timenorm, bool changeforce = false, bool forcechange = false)
        {
            // This if statement looks way too complicated... but i don't want to create extra functions just to make this a bit less... adds nothing
            // looks if any force from controller is 1% different from old force, same with the config force and the config world force, but also checks if rotation is off by more than 1 degree since last update
            if (forcechange || (changeforce && ((controller != null && (controllerForce - oldControllerForce).magnitude > 0.01 * controller.maxWind) || (newConfig.force - oldForce).magnitude > 0.01 * newConfig.force.magnitude || (newConfig.worldForce - oldWorldForce).magnitude > 0.01 * newConfig.worldForce.magnitude || (Quaternion.Angle(oldRot, vessel.transform.rotation) > 1 && (newConfig.worldForce.magnitude != 0 || controllerForce.magnitude != 0)))))
            {
                UpdateForce(oldConfig.force + (newConfig.force - oldConfig.force) * timenorm + unit.transform.InverseTransformDirection(oldConfig.worldForce + (newConfig.worldForce - oldConfig.worldForce) * timenorm + controllerForce), vessel, true, unit);
                return true;
            }
            // if we don't update the force we can update everything else... i hate this system so much...
            unit.worldVelocity = oldConfig.worldVelocity + (newConfig.worldVelocity - oldConfig.worldVelocity) * timenorm + controllerVelocity;
            unit.localVelocity = oldConfig.velocity + (newConfig.velocity - oldConfig.velocity) * timenorm;
            unit.maxEmission = (int)Math.Round(oldConfig.maxEmission + (newConfig.maxEmission - oldConfig.maxEmission) * timenorm);
            unit.maxSize = oldConfig.maxSize + (newConfig.maxSize - oldConfig.maxSize) * timenorm;
            unit.maxEnergy = oldConfig.maxEnergy + (newConfig.maxEnergy - oldConfig.maxEnergy) * timenorm;
            unit.minEmission = (int)Math.Round(oldConfig.minEmission + (newConfig.minEmission - oldConfig.minEmission) * timenorm);
            unit.minSize = oldConfig.minSize + (newConfig.minSize - oldConfig.minSize) * timenorm;
            unit.minEnergy = oldConfig.minEnergy + (newConfig.minEnergy - oldConfig.minEnergy) * timenorm;
            unit.rndForce = oldConfig.rndForce + (newConfig.rndForce - oldConfig.rndForce) * timenorm;
            unit.rndVelocity = oldConfig.rndVelocity + (newConfig.rndVelocity - oldConfig.rndVelocity) * timenorm;
            unit.shape1D = oldConfig.shape1D + (newConfig.shape1D - oldConfig.shape1D) * timenorm;
            unit.sizeGrow = oldConfig.sizeGrow + (newConfig.sizeGrow - oldConfig.sizeGrow) * timenorm;
            unit.maxParticleSize = oldConfig.maxParticleSize + (newConfig.maxParticleSize - oldConfig.maxParticleSize) * timenorm;
            if (newConfig.changeAtStart || timenorm <= 1)
                unit.useWorldSpace = newConfig.useWorldSpace;
            return false;
        }

        public void UpdateForce(Vector3 newforce, Vessel vessel, bool replaceforce = false, KSPParticleEmitter emitter = null)
        {
            Config savecfg = new Config(newConfig);
            newConfig = new Config(startConfig);
            // the usual route is recieving an emitter but to make this future proof i can also update all particles at once
            if (emitter != null)
            {
                UpdateParticle(emitter, vessel, 1);
                if (replaceforce)
                    emitter.force = newforce;
                else
                    emitter.force += newforce;
                emitter.SetDirty();
            }
            else
                foreach (KSPParticleEmitter unit in particle)
                {
                    UpdateParticle(unit, vessel, 1);
                    if (replaceforce)
                        unit.force = newforce;
                    else
                        unit.force += newforce;
                    unit.SetDirty();
                }
            oldStartTime = startTime;

            // Please do reduce this 0.05 seconds
            startTime = Planetarium.GetUniversalTime() + 0.05;
            newConfig = savecfg;
            return;
        }
    }

    public class ModulePekkaVent : PartModule
    {
        public const string groupName = "PekkaVent";
        const string TAG = "[ModulePekkaVent]";

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool debug = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool Ui = false;
        public Rect UiRect = new Rect(100, 100, 100, 1000);
        public Config UiConfig = new Config();

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool effects1Visible = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool effects2Visible = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool effects3Visible = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool effects4Visible = false;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string particles1Name = "effects1";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string particles2Name = "effects2";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string particles3Name = "effects3";
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string particles4Name = "effects4";

        [KSPField(isPersistant = true, guiActive = false, guiFormat = "N2", guiName = "WindStrength", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f)]
        public float WindStrength = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiFormat = "N2", guiName = "WindDirection", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0f, maxValue = 360f, stepIncrement = 5f)]
        public float WindDirection = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiFormat = "N2", guiName = "Controller", groupName = groupName, groupDisplayName = groupName)]
        [UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f)]
        public float slider = 0f;

        const string hideTime = "Untime vapor";
        const string showTime = "Time vapor";
        const string hide = "Hide ";
        const string show = "Show ";
        const string toggle = "Toggle ";
        const string VelocityActivate = "Next Config";
        const string VelocityDeactivate = "Previous Config";

        List<KSPParticleEmitter> emitter;
        List<KSPParticleEmitter> residualEmitter = new List<KSPParticleEmitter>();

        bool startEffects = true;
        bool start = true;
        double startTime = 0;
        bool effectsTimed = false;
        ConfigNode savedconfig;

        List<ParticleFX> effect1 = new List<ParticleFX>();
        bool effects1Controller = false;
        bool effects1Timed = false;
        bool effects1Change = false;
        bool effects1ActionActive = false;
        bool effects1HideEvent = false;
        double effects1Time;
        bool configchange1 = false;

        List<ParticleFX> effect2 = new List<ParticleFX>();
        bool effects2Controller = false;
        bool effects2Timed = false;
        bool effects2Change = false;
        bool effects2ActionActive = false;
        bool effects2HideEvent = false;
        double effects2Time;
        bool configchange2 = false;

        List<ParticleFX> effect3 = new List<ParticleFX>();
        bool effects3Controller = false;
        bool effects3Timed = false;
        bool effects3Change = false;
        bool effects3ActionActive = false;
        bool effects3HideEvent = false;
        double effects3Time;
        bool configchange3 = false;

        List<ParticleFX> effect4 = new List<ParticleFX>();
        bool effects4Controller = false;
        bool effects4Timed = false;
        bool effects4Change = false;
        bool effects4ActionActive = false;
        bool effects4HideEvent = false;
        double effects4Time;
        bool configchange4 = false;

        public int uiActive = 0;

#if Timed
        PerfTimer timer = new PerfTimer();
#endif

        // crazy Ksp stuff... all just duplicated because for some reason i can't just create a button on the fly... or don't know how to at least

        [KSPEvent(guiActive = true, guiName = show, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void ShowParticle1Event()
        {
            foreach (ParticleFX effect in effect1)
            {
                if (effects1Controller)
                    effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
            }
            effects1Visible = true;
            UpdateEvents1(true);
        }

        [KSPEvent(guiActive = true, guiName = hide, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void HideParticle1Event()
        {
            foreach (ParticleFX effect in effect1)
            {
                effect.HideParticleEvent(slider, WindStrength, WindDirection, effects1Change, vessel, debug);
                UpdateEvents1(false, effect.fadeOutTime != 0);
            }
            effects1Visible = false;
        }

        [KSPEvent(guiActive = true, guiName = show, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void ShowParticle2Event()
        {
            foreach (ParticleFX effect in effect2)
            {
                if (effects2Controller)
                    effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
            }
            effects2Visible = true;
            UpdateEvents2(true);
        }

        [KSPEvent(guiActive = true, guiName = hide, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void HideParticle2Event()
        {
            foreach (ParticleFX effect in effect2)
            {
                effect.HideParticleEvent(slider, WindStrength, WindDirection, effects2Change, vessel, debug);
                UpdateEvents2(false, effect.fadeOutTime != 0);
            }
            effects2Visible = false;
        }

        [KSPEvent(guiActive = true, guiName = show, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void ShowParticle3Event()
        {
            foreach (ParticleFX effect in effect3)
            {
                if (effects3Controller)
                    effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
            }
            effects3Visible = true;
            UpdateEvents3(true);
        }

        [KSPEvent(guiActive = true, guiName = hide, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void HideParticle3Event()
        {
            foreach (ParticleFX effect in effect3)
            {
                effect.HideParticleEvent(slider, WindStrength, WindDirection, effects3Change, vessel, debug);
                UpdateEvents3(false, effect.fadeOutTime != 0);
            }
            effects3Visible = false;
        }

        [KSPEvent(guiActive = true, guiName = show, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void ShowParticle4Event()
        {
            foreach (ParticleFX effect in effect4)
            {
                if (effects4Controller)
                    effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
            }
            effects4Visible = true;
            UpdateEvents4(true);
        }

        [KSPAction(hide)]
        [KSPEvent(guiActive = true, guiName = hide, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void HideParticle4Event()
        {
            foreach (ParticleFX effect in effect4)
            {
                effect.HideParticleEvent(slider, WindStrength, WindDirection, effects4Change, vessel, debug);
                UpdateEvents4(false, effect.fadeOutTime != 0);
            }
            effects4Visible = false;
        }

        [KSPAction(showTime)]
        [KSPEvent(guiActive = true, guiName = showTime, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void TimeSteamEvent()
        {
            if (effects1ActionActive)
            {
                effects1Timed = true;
                effects1Change = true;

                effects1Time = Planetarium.GetUniversalTime();

                foreach (ParticleFX effect in effect1)
                    if (effect.timedActions[effect.activeAction + 1].startTime == 0)
                    {
                        effect.NextActionEvent(slider, effects1Time, effects1Visible, WindStrength, WindDirection, vessel, debug);
                        effects1Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects1Visible;
                        UpdateEvents1(effects1Visible);
                    }
            }

            if (effects2ActionActive)
            {
                effects2Timed = true;
                effects2Change = true;

                effects2Time = Planetarium.GetUniversalTime();

                foreach (ParticleFX effect in effect2)
                    if (effect.timedActions[effect.activeAction + 1].startTime == 0)
                    {
                        effect.NextActionEvent(slider, effects2Time, effects2Visible, WindStrength, WindDirection, vessel, debug);
                        effects2Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects2Visible;
                        UpdateEvents2(effects2Visible);
                    }
            }

            if (effects3ActionActive)
            {
                effects3Timed = true;
                effects3Change = true;

                effects3Time = Planetarium.GetUniversalTime();

                foreach (ParticleFX effect in effect3)
                    if (effect.timedActions[effect.activeAction + 1].startTime == 0)
                    {
                        effect.NextActionEvent(slider, effects3Time, effects3Visible, WindStrength, WindDirection, vessel, debug);
                        effects3Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects3Visible;
                        UpdateEvents3(effects3Visible);
                    }
            }

            if (effects4ActionActive)
            {
                effects4Timed = true;
                effects4Change = true;

                effects4Time = Planetarium.GetUniversalTime();

                foreach (ParticleFX effect in effect4)
                    if (effect.timedActions[effect.activeAction + 1].startTime == 0)
                    {
                        effect.NextActionEvent(slider, effects4Time, effects4Visible, WindStrength, WindDirection, vessel, debug);
                        effects4Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects4Visible;
                        UpdateEvents4(effects4Visible);
                    }
            }

            UpdateEventsTiming(true);
        }

        [KSPAction(hideTime)]
        [KSPEvent(guiActive = true, guiName = hideTime, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void UntimeSteamEvent()
        {
            effects1Timed = false;
            effects1Change = false;

            foreach (ParticleFX effect in effect1)
                effect.activeAction = -1;

            effects2Timed = false;
            effects2Change = false;

            foreach (ParticleFX effect in effect2)
                effect.activeAction = -1;

            effects3Timed = false;
            effects3Change = false;

            foreach (ParticleFX effect in effect3)
                effect.activeAction = -1;

            effects4Timed = false;
            effects4Change = false;

            foreach (ParticleFX effect in effect4)
                effect.activeAction = -1;

            UpdateEventsTiming(effects1Timed);
        }

        [KSPAction(VelocityActivate)]
        [KSPEvent(guiActive = true, guiName = VelocityActivate, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void NextConfig()
        {
            foreach (ParticleFX effect in effect1)
            {
                effect.NextConfigEvent();

                if (effects1Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects1Change = true;

            foreach (ParticleFX effect in effect2)
            {
                effect.NextConfigEvent();

                if (effects2Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects2Change = true;

            foreach (ParticleFX effect in effect3)
            {
                effect.NextConfigEvent();

                if (effects3Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects3Change = true;

            foreach (ParticleFX effect in effect4)
            {
                effect.NextConfigEvent();

                if (effects4Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects4Change = true;

            Events["NextConfig"].active = false;
        }

        [KSPAction(VelocityDeactivate)]
        [KSPEvent(guiActive = true, guiName = VelocityDeactivate, active = true, groupName = groupName, groupDisplayName = groupName)]
        public void PreviousConfig()
        {
            foreach (ParticleFX effect in effect1)
            {
                effect.PreviousConfigEvent();

                if (effects1Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects1Change = true;

            foreach (ParticleFX effect in effect2)
            {
                effect.PreviousConfigEvent();

                if (effects2Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects2Change = true;

            foreach (ParticleFX effect in effect3)
            {
                effect.PreviousConfigEvent();

                if (effects3Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects3Change = true;

            foreach (ParticleFX effect in effect4)
            {
                effect.PreviousConfigEvent();

                if (effects4Change)
                {
                    effect.oldConfig = effect.Interpolate(effect.newConfig, effect.oldConfig, effect.Scalar());
                    effect.changeTime = effect.oldConfig.changeTime;
                }
            }
            effects4Change = true;

            Events["PreviousConfig"].active = false;
        }

        [KSPAction(toggle)]
        public void ToggleParticle1Event(KSPActionParam param)
        {
            if (!effects1Visible)
                foreach (ParticleFX effect in effect1)
                {
                    if (effects1Controller)
                        effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                    effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
                }
            else
                foreach (ParticleFX effect in effect1)
                    effect.HideParticleEvent(slider, WindStrength, WindDirection, effects1Change, vessel, debug);

            effects1Visible = !effects1Visible;
            UpdateEvents1(effects1Visible, !effects1Visible && effect1[0].fadeOutTime != 0);
        }

        [KSPAction(toggle)]
        public void ToggleParticle2Event(KSPActionParam param)
        {
            if (!effects2Visible)
                foreach (ParticleFX effect in effect2)
                {
                    if (effects2Controller)
                        effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                    effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
                }
            else
                foreach (ParticleFX effect in effect2)
                    effect.HideParticleEvent(slider, WindStrength, WindDirection, effects1Change, vessel, debug);

            effects2Visible = !effects2Visible;
            UpdateEvents2(effects2Visible, !effects2Visible && effect2[0].fadeOutTime != 0);
        }

        [KSPAction(toggle)]
        public void ToggleParticle3Event(KSPActionParam param)
        {
            if (!effects3Visible)
                foreach (ParticleFX effect in effect3)
                {
                    if (effects3Controller)
                        effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                    effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
                }
            else
                foreach (ParticleFX effect in effect3)
                    effect.HideParticleEvent(slider, WindStrength, WindDirection, effects3Change, vessel, debug);

            effects3Visible = !effects3Visible;
            UpdateEvents1(effects3Visible, !effects3Visible && effect3[0].fadeOutTime != 0);
        }

        [KSPAction(toggle)]
        public void ToggleParticle4Event(KSPActionParam param)
        {
            if (!effects4Visible)
                foreach (ParticleFX effect in effect4)
                {
                    if (effects4Controller)
                        effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug);

                    effect.ShowParticleEvent(WindStrength, WindDirection, vessel, debug);
                }
            else
                foreach (ParticleFX effect in effect4)
                    effect.HideParticleEvent(slider, WindStrength, WindDirection, effects1Change, vessel, debug);

            effects4Visible = !effects4Visible;
            UpdateEvents1(effects4Visible, !effects4Visible && effect4[0].fadeOutTime != 0);
        }

        private void SetupUI()
        {
            Events["ShowParticle1Event"].guiName = show + particles1Name;
            Events["HideParticle1Event"].guiName = hide + particles1Name;

            Events["ShowParticle2Event"].guiName = show + particles2Name;
            Events["HideParticle2Event"].guiName = hide + particles2Name;

            Events["ShowParticle3Event"].guiName = show + particles3Name;
            Events["HideParticle3Event"].guiName = hide + particles3Name;

            Events["ShowParticle4Event"].guiName = show + particles4Name;
            Events["HideParticle4Event"].guiName = hide + particles4Name;

            Actions["ToggleParticle1Event"].guiName = toggle + particles1Name;
            Actions["ToggleParticle2Event"].guiName = toggle + particles2Name;
            Actions["ToggleParticle3Event"].guiName = toggle + particles3Name;
            Actions["ToggleParticle4Event"].guiName = toggle + particles4Name;
        }

        private void UpdateEvents1(bool visible, bool slow = false, bool inactive = false)
        {
            Events["ShowParticle1Event"].active = !visible;
            Events["HideParticle1Event"].active = visible;
            if (effect1.Count == 0 || slow || inactive)
            {
                Events["ShowParticle1Event"].active = false;
                Events["HideParticle1Event"].active = false;
            }
            if (slow)
            {
                effects1Change = true;
                effects1HideEvent = true;
            }
        }

        private void UpdateEvents2(bool visible, bool slow = false, bool inactive = false)
        {
            Events["ShowParticle2Event"].active = !visible;
            Events["HideParticle2Event"].active = visible;
            if (effect2.Count == 0 || slow || inactive)
            {
                Events["ShowParticle2Event"].active = false;
                Events["HideParticle2Event"].active = false;
            }
            if (slow)
            {
                effects2Change = true;
                effects2HideEvent = true;
            }
        }

        private void UpdateEvents3(bool visible, bool slow = false, bool inactive = false)
        {
            Events["ShowParticle3Event"].active = !visible;
            Events["HideParticle3Event"].active = visible;
            if (effect3.Count == 0 || slow || inactive)
            {
                Events["ShowParticle3Event"].active = false;
                Events["HideParticle3Event"].active = false;
            }
            if (slow)
            {
                effects3Change = true;
                effects3HideEvent = true;
            }
        }

        private void UpdateEvents4(bool visible, bool slow = false, bool inactive = false)
        {
            Events["ShowParticle4Event"].active = !visible;
            Events["HideParticle4Event"].active = visible;
            if (effect4.Count == 0 || slow || inactive)
            {
                Events["ShowParticle4Event"].active = false;
                Events["HideParticle4Event"].active = false;
            }
            if (slow)
            {
                effects4Change = true;
                effects4HideEvent = true;
            }
        }

        private void UpdateEventsTiming(bool timed)
        {
            Events["TimeSteamEvent"].active = (!timed && effectsTimed);
            Events["UntimeSteamEvent"].active = (timed && effectsTimed);
            UpdateEventsConfig(!timed && ((configchange1 && effects1Visible) || (configchange2 && effects2Visible) || (configchange3 && effects3Visible) || (configchange4 && effects4Visible)));
        }

        private void UpdateEventsConfig(bool show)
        {
            Events["NextConfig"].active = show;
            Events["PreviousConfig"].active = show;
        }

        public void FixedUpdate()
        {
#if Timed
            timer.Start();
#endif

            //watchtime++;
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (startEffects)
                {
                    // at the start it can't update the particle or it will freak out so it waits 0.5 seconds after loading
                    if (Planetarium.GetUniversalTime() > startTime + 0.5)
                    {
                        startEffects = false;
                        UpdateWind(true);
                        UpdateWind(false);
                        this.Log(debug, "updating " + part.name + " finally");
                    }
                }
                else //this gets the controllers up and shows the effects at start
                {
                    if (effect1.Count > 0)
                    {
                        if (effects1Visible && start)
                            foreach (ParticleFX effect in effect1)
                                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, start);
                        else if ((effects1Visible || (effect1[0].controller != null && (effect1[0].controller.startupEffect || effect1[0].controller.shutdownEffect))) && effects1Controller)
                            foreach (ParticleFX effect in effect1)
                                effects1HideEvent = effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug) ? true : effects1HideEvent;
                    }

                    if (effect2.Count > 0)
                    {
                        if (effects2Visible && start)
                            foreach (ParticleFX effect in effect2)
                                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, start);
                        else if ((effects2Visible || (effect2[0].controller != null && (effect2[0].controller.startupEffect || effect2[0].controller.shutdownEffect))) && effects2Controller)
                            foreach (ParticleFX effect in effect2)
                                effects2HideEvent = effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug) ? true : effects2HideEvent;
                    }

                    if (effect3.Count > 0)
                    {
                        if (effects3Visible && start)
                            foreach (ParticleFX effect in effect3)
                                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, start);
                        else if ((effects3Visible || (effect3[0].controller != null && (effect3[0].controller.startupEffect || effect3[0].controller.shutdownEffect))) && effects3Controller)
                            foreach (ParticleFX effect in effect3)
                                effects3HideEvent = effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug) ? true : effects3HideEvent;
                    }

                    if (effect4.Count > 0)
                    {
                        if (effects4Visible && start)
                            foreach (ParticleFX effect in effect4)
                                effect.ShowParticleEvent(WindStrength, WindDirection, vessel, start);
                        else if ((effects4Visible || (effect4[0].controller != null && (effect4[0].controller.startupEffect || effect4[0].controller.shutdownEffect))) && effects4Controller)
                            foreach (ParticleFX effect in effect4)
                                effects4HideEvent = effect.ControlEffect(slider, WindStrength, WindDirection, vessel, debug) ? true : effects4HideEvent;
                    }

                    if (effects1HideEvent)
                        effects1Change = true;
                    if (effects2HideEvent)
                        effects2Change = true;
                    if (effects3HideEvent)
                        effects3Change = true;
                    if (effects4HideEvent)
                        effects4Change = true;

                    if (start)
                        start = false;
                }

                // this is for the timed effects
                double ut = Planetarium.GetUniversalTime();
                if (effects1Change)
                {
                    if (effects1Timed)
                    {
                        bool stoptiming = true;
                        foreach (ParticleFX effect in effect1)
                        {
                            if (effect.activeAction == -1 || effects1Time + effect.timedActions[effect.activeAction].endTime < ut)
                            {
                                if (effect.timedActions.Count > effect.activeAction + 1)
                                    stoptiming = false;
                                if (effect.timedActions.Count > effect.activeAction + 1 && effect.timedActions[effect.activeAction + 1].startTime + effects1Time <= ut)
                                {
                                    effects1HideEvent = effect.NextActionEvent(slider, effects1Time, effects1Visible, WindStrength, WindDirection, vessel, debug);
                                    effects1Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects1Visible;
                                    UpdateEvents1(effects1Visible);
                                }
                            }
                            else
                                stoptiming = false;
                        }
                        if (stoptiming)
                        {
                            foreach (ParticleFX effect in effect1)
                                effect.activeAction = -1;
                            effects1Timed = false;
                            UpdateEventsTiming(effects1Timed || effects2Timed || effects3Timed || effects4Timed);
                        }
                    }

                    bool stopeffect = true;
                    foreach (ParticleFX effect in effect1)
                    {
                        effect.ChangeEffect(vessel, 0, false, true);
                        if (effect.startTime + effect.changeTime > ut)
                            stopeffect = false;
                    }
                    if (stopeffect)
                    {
                        if (effects1HideEvent)
                        {
                            foreach (ParticleFX effect in effect1)
                                effect.HideParticleEvent(slider, WindStrength, WindDirection, false, vessel, debug, true);
                            UpdateEvents1(effects1Visible);
                            effects1HideEvent = false;
                        }
                        effects1Change = effects1Timed;
                        UpdateEventsConfig((configchange1 && effects1Visible) || (configchange2 && effects2Visible) || (configchange3 && effects3Visible) || (configchange4 && effects4Visible));
                    }
                }
                if (effects2Change)
                {
                    if (effects2Timed)
                    {
                        bool stoptiming = true;
                        foreach (ParticleFX effect in effect2)
                        {
                            if (effect.activeAction == -1 || effects2Time + effect.timedActions[effect.activeAction].endTime < ut)
                            {
                                if (effect.timedActions.Count > effect.activeAction + 1)
                                    stoptiming = false;
                                if (effect.timedActions.Count > effect.activeAction + 1 && effect.timedActions[effect.activeAction + 1].startTime + effects2Time <= ut)
                                {
                                    effects2HideEvent = effect.NextActionEvent(slider, effects2Time, effects2Visible, WindStrength, WindDirection, vessel, debug);
                                    effects2Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects2Visible;
                                    UpdateEvents2(effects2Visible);
                                }
                            }
                            else
                                stoptiming = false;
                        }
                        if (stoptiming)
                        {
                            foreach (ParticleFX effect in effect2)
                                effect.activeAction = -1;
                            effects2Timed = false;
                            UpdateEventsTiming(effects1Timed || effects2Timed || effects3Timed || effects4Timed);
                        }
                    }

                    bool stopeffect = true;
                    foreach (ParticleFX effect in effect2)
                    {
                        effect.ChangeEffect(vessel, 0, false, true);
                        if (effect.startTime + effect.changeTime > ut)
                            stopeffect = false;
                    }
                    if (stopeffect)
                    {
                        if (effects2HideEvent)
                        {
                            foreach (ParticleFX effect in effect2)
                                effect.HideParticleEvent(slider, WindStrength, WindDirection, false, vessel, debug, true);
                            UpdateEvents2(effects2Visible);
                            effects2HideEvent = false;
                        }
                        effects2Change = effects2Timed;
                        UpdateEventsConfig((configchange1 && effects1Visible) || (configchange2 && effects2Visible) || (configchange3 && effects3Visible) || (configchange4 && effects4Visible));
                    }
                }
                if (effects3Change)
                {
                    if (effects3Timed)
                    {
                        bool stoptiming = true;
                        foreach (ParticleFX effect in effect3)
                        {
                            if (effect.activeAction == -1 || effects3Time + effect.timedActions[effect.activeAction].endTime < ut)
                            {
                                if (effect.timedActions.Count > effect.activeAction + 1)
                                    stoptiming = false;
                                if (effect.timedActions.Count > effect.activeAction + 1 && effect.timedActions[effect.activeAction + 1].startTime + effects3Time <= ut)
                                {
                                    effects3HideEvent = effect.NextActionEvent(slider, effects3Time, effects3Visible, WindStrength, WindDirection, vessel, debug);
                                    effects3Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects3Visible;
                                    UpdateEvents3(effects3Visible);
                                }
                            }
                            else
                                stoptiming = false;
                        }
                        if (stoptiming)
                        {
                            foreach (ParticleFX effect in effect3)
                                effect.activeAction = -1;
                            effects3Timed = false;
                            UpdateEventsTiming(effects1Timed || effects2Timed || effects3Timed || effects4Timed);
                        }
                    }

                    bool stopeffect = true;
                    foreach (ParticleFX effect in effect3)
                    {
                        effect.ChangeEffect(vessel, 0, false, true);
                        if (effect.startTime + effect.changeTime > ut)
                            stopeffect = false;
                    }
                    if (stopeffect)
                    {
                        if (effects3HideEvent)
                        {
                            foreach (ParticleFX effect in effect3)
                                effect.HideParticleEvent(slider, WindStrength, WindDirection, false, vessel, debug, true);
                            UpdateEvents3(effects3Visible);
                            effects3HideEvent = false;
                        }
                        effects3Change = effects3Timed;
                        UpdateEventsConfig((configchange1 && effects1Visible) || (configchange2 && effects2Visible) || (configchange3 && effects3Visible) || (configchange4 && effects4Visible));
                    }
                }
                if (effects4Change)
                {
                    if (effects4Timed)
                    {
                        bool stoptiming = true;
                        foreach (ParticleFX effect in effect4)
                        {
                            if (effect.activeAction == -1 || effects4Time + effect.timedActions[effect.activeAction].endTime < ut)
                            {
                                if (effect.timedActions.Count > effect.activeAction + 1)
                                    stoptiming = false;
                                if (effect.timedActions.Count > effect.activeAction + 1 && effect.timedActions[effect.activeAction + 1].startTime + effects4Time <= ut)
                                {
                                    effects4HideEvent = effect.NextActionEvent(slider, effects4Time, effects4Visible, WindStrength, WindDirection, vessel, debug);
                                    effects4Visible = effect.timedActions[effect.activeAction].toggleActive ^ effects4Visible;
                                    UpdateEvents4(effects4Visible);
                                }
                            }
                            else
                                stoptiming = false;
                        }
                        if (stoptiming)
                        {
                            foreach (ParticleFX effect in effect4)
                                effect.activeAction = -1;
                            effects4Timed = false;
                            UpdateEventsTiming(effects1Timed || effects2Timed || effects3Timed || effects4Timed);
                        }
                    }

                    bool stopeffect = true;
                    foreach (ParticleFX effect in effect4)
                    {
                        effect.ChangeEffect(vessel, 0, false, true);
                        if (effect.startTime + effect.changeTime > ut)
                            stopeffect = false;
                    }
                    if (stopeffect)
                    {
                        if (effects4HideEvent)
                        {
                            foreach (ParticleFX effect in effect4)
                                effect.HideParticleEvent(slider, WindStrength, WindDirection, false, vessel, debug, true);
                            UpdateEvents4(effects4Visible);
                            effects4HideEvent = false;
                        }
                        effects4Change = effects4Timed;
                        UpdateEventsConfig((configchange1 && effects1Visible) || (configchange2 && effects2Visible) || (configchange3 && effects3Visible) || (configchange4 && effects4Visible));
                    }
                }
            }

#if Timed
            this.Log(true, $"{timer.Stop()} for FixedUpdate on {part.name}");
#endif
        }

        // this updates wind strength and direction of every other part on vessel if it gets modified
        // there should be a better way to do this because it is being called a lot when updating the wind
        // but wind doesn't get updated very often so its fine for now
        public void UpdateWind(bool isSpeed)
        {
            foreach (Part unit in vessel.parts)
                if (unit.FindModuleImplementing<ModulePekkaVent>() != null)
                    if (isSpeed)
                        unit.FindModuleImplementing<ModulePekkaVent>().WindStrength = WindStrength;
                    else
                        unit.FindModuleImplementing<ModulePekkaVent>().WindDirection = WindDirection;
        }

        public void setupConfigNode(ConfigNode setup)
        {
            // this seems to be way too long to just set everything up because a lot of things are the same... but i can't seem to find a better way
            foreach (ConfigNode node in setup.nodes.GetNodes("PARTICLE"))
            {
                ParticleFX newFX = new ParticleFX();
                newFX.particles = int.Parse(node.GetValue("particles"));
                // this uses the default value if it can't find the value in config otherwise it uses the value in config
                // it's replicated a lot...
                newFX.fadeInTime = (node.GetValue("fadeInTime") == null) ? newFX.fadeInTime : int.Parse(node.GetValue("fadeInTime"));
                newFX.fadeOutTime = (node.GetValue("fadeOutTime") == null) ? newFX.fadeOutTime : int.Parse(node.GetValue("fadeOutTime"));

                foreach (KSPParticleEmitter Particle in emitter)
                {
                    if (Particle.maxEmission == newFX.particles)
                    {
                        newFX.particle.Add(Particle);

                        residualEmitter.Remove(Particle);
                    }
                    this.Log(debug, Particle.maxEmission.ToString());
                }
                newFX.startConfig.CreateConfig(newFX.particle[0]);

                if (node.GetNode("CONTROLLER") != null)
                {
                    ConfigNode controller = node.GetNode("CONTROLLER");
                    newFX.controller = new Controller();

                    newFX.controller.engineController = (controller.GetValue("engineController") == null) ? newFX.controller.engineController : bool.Parse(controller.GetValue("engineController"));
                    newFX.controller.useAtm = (controller.GetValue("useAtm") == null) ? newFX.controller.useAtm : bool.Parse(controller.GetValue("useAtm"));
                    newFX.controller.useWind = (controller.GetValue("useWind") == null) ? newFX.controller.useWind : bool.Parse(controller.GetValue("useWind"));
                    newFX.controller.useThrust = (controller.GetValue("useThrust") == null) ? newFX.controller.useThrust : bool.Parse(controller.GetValue("useThrust"));
                    newFX.controller.useSpeed = (controller.GetValue("useSpeed") == null) ? newFX.controller.useSpeed : bool.Parse(controller.GetValue("useSpeed"));

                    newFX.controller.endTransform = (controller.GetValue("endTransform") == null) ? newFX.controller.endTransform : part.FindModelTransform(controller.GetValue("endTransform"));
                    newFX.controller.orientation = (controller.GetValue("orientation") == null) ? newFX.controller.orientation : controller.GetValue("orientation");
                    newFX.controller.axis = (controller.GetValue("axis") == null) ? newFX.controller.axis : controller.GetValue("axis");

                    newFX.controller.thrustStartCfg = (controller.GetValue("thrustStartCfg") == null) ? newFX.controller.thrustStartCfg : int.Parse(controller.GetValue("thrustStartCfg"));
                    newFX.controller.thrustEndCfg = (controller.GetValue("thrustEndCfg") == null) ? newFX.controller.thrustEndCfg : int.Parse(controller.GetValue("thrustEndCfg"));

                    newFX.controller.hideEffects = (controller.GetValue("hideEffects") == null) ? newFX.controller.hideEffects : bool.Parse(controller.GetValue("hideEffects"));
                    newFX.controller.hideSize = (controller.GetValue("hideSize") == null) ? newFX.controller.hideSize : float.Parse(controller.GetValue("hideSize"));

                    if (newFX.controller.useAtm || newFX.controller.engineController || newFX.controller.useThrust || newFX.controller.useSpeed || newFX.controller.useWind)
                    {
                        if (newFX.controller.engineController)
                        {
                            if (newFX.controller.engineController)
                                newFX.controller.engine = part.FindModuleImplementing<ModuleEnginesFX>();
                            newFX.controller.startupEffect = (controller.GetValue("startupEffect") == null) ? newFX.controller.startupEffect : bool.Parse(controller.GetValue("startupEffect"));
                            newFX.controller.startupTime = (controller.GetValue("startupTime") == null) ? newFX.controller.startupTime : float.Parse(controller.GetValue("startupTime"));
                            newFX.controller.shutdownEffect = (controller.GetValue("shutdownEffect") == null) ? newFX.controller.shutdownEffect : bool.Parse(controller.GetValue("shutdownEffect"));
                            newFX.controller.shutdownTime = (controller.GetValue("shutdownTime") == null) ? newFX.controller.shutdownTime : float.Parse(controller.GetValue("shutdownTime"));
                        }

                        if (newFX.controller.useThrust)
                        {
                            newFX.controller.collectorTransform = (controller.GetValue("collectorTransform") == null) ? newFX.controller.collectorTransform : part.FindModelTransform(controller.GetValue("collectorTransform"));
                            newFX.controller.collectorRadius = (controller.GetValue("collectorRadius") == null) ? newFX.controller.collectorRadius : float.Parse(controller.GetValue("collectorRadius"));
                            newFX.controller.thrustPower = (controller.GetValue("thrustPower") == null) ? newFX.controller.thrustPower : double.Parse(controller.GetValue("thrustPower"));
                            newFX.controller.distancePower = (controller.GetValue("distancePower") == null) ? newFX.controller.distancePower : double.Parse(controller.GetValue("distancePower"));
                            if (controller.GetNode("THRUSTCURVE") != null)
                                foreach (ConfigNode curve in controller.nodes.GetNodes("THRUSTCURVE"))
                                {
                                    newFX.controller.thrustCurve = new Curve();
                                    string[] strings = curve.GetValues("key");

                                    foreach (string String in strings)
                                        newFX.controller.thrustCurve.AddPoint(String);
                                    newFX.controller.thrustCurve.SortCurve();
                                }
                            else
                            {
                                newFX.controller.endPower = (controller.GetValue("endPower") == null) ? newFX.controller.endPower : double.Parse(controller.GetValue("endPower"));
                                newFX.controller.startThreshold = (controller.GetValue("startThreshold") == null) ? newFX.controller.startThreshold : double.Parse(controller.GetValue("startThreshold"));
                                newFX.controller.endThreshold = (controller.GetValue("endThreshold") == null) ? newFX.controller.endThreshold : double.Parse(controller.GetValue("endThreshold"));
                            }
                        }

                        if (newFX.controller.useAtm)
                        {
                            newFX.controller.atmStartCfg = (controller.GetValue("atmStartCfg") == null) ? newFX.controller.atmStartCfg : int.Parse(controller.GetValue("atmStartCfg"));
                            newFX.controller.atmEndCfg = (controller.GetValue("atmEndCfg") == null) ? newFX.controller.atmEndCfg : int.Parse(controller.GetValue("atmEndCfg"));
                            foreach (ConfigNode curve in controller.nodes.GetNodes("ATMCURVE"))
                            {
                                newFX.controller.atmCurve = new Curve();
                                string[] strings = curve.GetValues("key");

                                foreach (string String in strings)
                                    newFX.controller.atmCurve.AddPoint(String);
                                newFX.controller.atmCurve.SortCurve();
                            }
                        }

                        if (newFX.controller.useSpeed)
                        {
                            newFX.controller.speedIsForce = (controller.GetValue("isForce") == null) ? newFX.controller.speedIsForce : bool.Parse(controller.GetValue("speedIsForce"));
                            newFX.controller.speedPower = (controller.GetValue("speedPower") == null) ? newFX.controller.speedPower : float.Parse(controller.GetValue("speedPower"));
                            newFX.controller.speedMultiplier = (controller.GetValue("speedMultiplier") == null) ? newFX.controller.speedMultiplier : float.Parse(controller.GetValue("speedMultiplier"));
                            newFX.controller.atmPower = (controller.GetValue("atmPower") == null) ? newFX.controller.atmPower : float.Parse(controller.GetValue("atmPower"));
                        }

                        if (newFX.controller.useWind)
                        {
                            newFX.controller.windIsForce = (controller.GetValue("windIsForce") == null) ? newFX.controller.windIsForce : bool.Parse(controller.GetValue("windIsForce"));
                            newFX.controller.maxWind = (controller.GetValue("maxWind") == null) ? newFX.controller.maxWind : float.Parse(controller.GetValue("maxWind"));

                            Fields.TryGetFieldUIControl("WindStrength", out UI_FloatRange WindField);
                            WindField.onFieldChanged = delegate { UpdateWind(true); };

                            Fields.TryGetFieldUIControl("WindDirection", out UI_FloatRange DirectionField);
                            DirectionField.onFieldChanged = delegate { UpdateWind(false); };

                            Fields["WindStrength"].guiActive = true;
                            Fields["WindDirection"].guiActive = true;
                            WindStrength = 0;
                            WindDirection = 0;
                        }
                    }
                    else
                        Fields["slider"].guiActive = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 1)
                        effects1Controller = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 2)
                        effects2Controller = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 3)
                        effects3Controller = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 4)
                        effects4Controller = true;

                    Events["NextConfig"].active = false;
                    Events["PreviousConfig"].active = false;
                }
                foreach (ConfigNode Action in node.nodes.GetNodes("TIMEDACTION"))
                {
                    TimedAction newAction = new TimedAction();

                    newAction.toggleActive = (Action.GetValue("toggleActive") == null) ? newAction.toggleActive : bool.Parse(Action.GetValue("toggleActive"));
                    newAction.moveToCfg = (Action.GetValue("moveToCfg") == null) ? newAction.moveToCfg : int.Parse(Action.GetValue("moveToCfg"));
                    newAction.startTime = (Action.GetValue("startTime") == null) ? newAction.startTime : float.Parse(Action.GetValue("startTime"));
                    newAction.endTime = (Action.GetValue("endTime") == null) ? newAction.endTime : float.Parse(Action.GetValue("endTime"));

                    newFX.timedActions.Add(newAction);

                    if (int.Parse(node.GetValue("activeGroup")) == 1)
                        effects1ActionActive = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 2)
                        effects2ActionActive = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 3)
                        effects3ActionActive = true;

                    if (int.Parse(node.GetValue("activeGroup")) == 4)
                        effects4ActionActive = true;
                    effectsTimed = true;
                }
                foreach (ConfigNode NodeConfig in node.nodes.GetNodes("CONFIG"))
                {
                    Config newConfig = new Config();
                    newConfig.cfgNumber = int.Parse(NodeConfig.GetValue("cfgNumber"));

                    if (newConfig.cfgNumber == 0)
                    {
                        newConfig.CreateConfig(newFX.particle[0]);
                        newFX.oldForce = newConfig.force;
                    }

                    newConfig.changeAtStart = (NodeConfig.GetValue("changeAtStart") == null) ? newConfig.changeAtStart : bool.Parse(NodeConfig.GetValue("changeAtStart"));
                    newConfig.useWorldSpace = (NodeConfig.GetValue("useWorldSpace") == null) ? newConfig.useWorldSpace : bool.Parse(NodeConfig.GetValue("useWorldSpace"));

                    if (NodeConfig.GetValue("useConfig") != null)
                    {
                        newConfig = new Config(newFX.configs[int.Parse(NodeConfig.GetValue("useConfig"))]);
                    }

                    if (NodeConfig.GetValue("changeFromConfig") != null && bool.Parse(NodeConfig.GetValue("changeFromConfig")))
                    {
                        newConfig.shape1D = (NodeConfig.GetValue("shape1D") == null) ? newConfig.shape1D : newConfig.shape1D + float.Parse(NodeConfig.GetValue("shape1D"));
                        newConfig.minSize = (NodeConfig.GetValue("minSize") == null) ? newConfig.minSize : newConfig.minSize + float.Parse(NodeConfig.GetValue("minSize"));
                        newConfig.maxSize = (NodeConfig.GetValue("maxSize") == null) ? newConfig.maxSize : newConfig.maxSize + float.Parse(NodeConfig.GetValue("maxSize"));
                        newConfig.maxParticleSize = (NodeConfig.GetValue("maxParticleSize") == null) ? newConfig.maxParticleSize : newConfig.maxParticleSize + float.Parse(NodeConfig.GetValue("maxParticleSize"));
                        newConfig.minEnergy = (NodeConfig.GetValue("minEnergy") == null) ? newConfig.minEnergy : newConfig.minEnergy + float.Parse(NodeConfig.GetValue("minEnergy"));
                        newConfig.maxEnergy = (NodeConfig.GetValue("maxEnergy") == null) ? newConfig.maxEnergy : newConfig.maxEnergy + float.Parse(NodeConfig.GetValue("maxEnergy"));
                        newConfig.minEmission = (NodeConfig.GetValue("minEmission") == null) ? newConfig.minEmission : newConfig.minEmission + int.Parse(NodeConfig.GetValue("minEmission"));
                        newConfig.maxEmission = (NodeConfig.GetValue("maxEmission") == null) ? newConfig.maxEmission : newConfig.maxEmission + int.Parse(NodeConfig.GetValue("maxEmission"));
                        newConfig.sizeGrow = (NodeConfig.GetValue("sizeGrow") == null) ? newConfig.sizeGrow : newConfig.sizeGrow + float.Parse(NodeConfig.GetValue("sizeGrow"));
                        newConfig.worldVelocity = (NodeConfig.GetValue("worldVelocity") == null) ? newConfig.worldVelocity : newConfig.worldVelocity + ConfigNode.ParseVector3(NodeConfig.GetValue("worldVelocity"));
                        newConfig.velocity = (NodeConfig.GetValue("velocity") == null) ? newConfig.velocity : newConfig.velocity + ConfigNode.ParseVector3(NodeConfig.GetValue("velocity"));
                        newConfig.rndVelocity = (NodeConfig.GetValue("rndVelocity") == null) ? newConfig.rndVelocity : newConfig.rndVelocity + ConfigNode.ParseVector3(NodeConfig.GetValue("rndVelocity"));
                        newConfig.worldForce = (NodeConfig.GetValue("worldForce") == null) ? newConfig.worldForce : newConfig.worldForce + ConfigNode.ParseVector3(NodeConfig.GetValue("worldForce"));
                        newConfig.force = (NodeConfig.GetValue("force") == null) ? newConfig.force : newConfig.force + ConfigNode.ParseVector3(NodeConfig.GetValue("force"));
                        newConfig.rndForce = (NodeConfig.GetValue("rndForce") == null) ? newConfig.rndForce : newConfig.rndForce + ConfigNode.ParseVector3(NodeConfig.GetValue("rndForce"));
                        newConfig.changeTime = (NodeConfig.GetValue("changeTime") == null) ? newConfig.changeTime : newConfig.changeTime + float.Parse(NodeConfig.GetValue("changeTime"));
                    }
                    else
                    {
                        newConfig.shape1D = (NodeConfig.GetValue("shape1D") == null) ? newConfig.shape1D : float.Parse(NodeConfig.GetValue("shape1D"));
                        newConfig.minSize = (NodeConfig.GetValue("minSize") == null) ? newConfig.minSize : float.Parse(NodeConfig.GetValue("minSize"));
                        newConfig.maxSize = (NodeConfig.GetValue("maxSize") == null) ? newConfig.maxSize : float.Parse(NodeConfig.GetValue("maxSize"));
                        newConfig.maxParticleSize = (NodeConfig.GetValue("maxParticleSize") == null) ? newConfig.maxParticleSize : float.Parse(NodeConfig.GetValue("maxParticleSize"));
                        newConfig.minEnergy = (NodeConfig.GetValue("minEnergy") == null) ? newConfig.minEnergy : float.Parse(NodeConfig.GetValue("minEnergy"));
                        newConfig.maxEnergy = (NodeConfig.GetValue("maxEnergy") == null) ? newConfig.maxEnergy : float.Parse(NodeConfig.GetValue("maxEnergy"));
                        newConfig.minEmission = (NodeConfig.GetValue("minEmission") == null) ? newConfig.minEmission : int.Parse(NodeConfig.GetValue("minEmission"));
                        newConfig.maxEmission = (NodeConfig.GetValue("maxEmission") == null) ? newConfig.maxEmission : int.Parse(NodeConfig.GetValue("maxEmission"));
                        newConfig.sizeGrow = (NodeConfig.GetValue("sizeGrow") == null) ? newConfig.sizeGrow : float.Parse(NodeConfig.GetValue("sizeGrow"));
                        newConfig.worldVelocity = (NodeConfig.GetValue("worldVelocity") == null) ? newConfig.worldVelocity : ConfigNode.ParseVector3(NodeConfig.GetValue("worldVelocity"));
                        newConfig.velocity = (NodeConfig.GetValue("velocity") == null) ? newConfig.velocity : ConfigNode.ParseVector3(NodeConfig.GetValue("velocity"));
                        newConfig.rndVelocity = (NodeConfig.GetValue("rndVelocity") == null) ? newConfig.rndVelocity : ConfigNode.ParseVector3(NodeConfig.GetValue("rndVelocity"));
                        newConfig.worldForce = (NodeConfig.GetValue("worldForce") == null) ? newConfig.worldForce : ConfigNode.ParseVector3(NodeConfig.GetValue("worldForce"));
                        newConfig.force = (NodeConfig.GetValue("force") == null) ? newConfig.force : ConfigNode.ParseVector3(NodeConfig.GetValue("force"));
                        newConfig.rndForce = (NodeConfig.GetValue("rndForce") == null) ? newConfig.rndForce : ConfigNode.ParseVector3(NodeConfig.GetValue("rndForce"));
                        newConfig.changeTime = (NodeConfig.GetValue("changeTime") == null) ? newConfig.changeTime : float.Parse(NodeConfig.GetValue("changeTime"));
                    }

                    newConfig.shape1D = (NodeConfig.GetValue("shape1DScale") == null) ? newConfig.shape1D : newConfig.shape1D * float.Parse(NodeConfig.GetValue("shape1DScale"));
                    newConfig.minSize = (NodeConfig.GetValue("minSizeScale") == null) ? newConfig.minSize : newConfig.minSize * float.Parse(NodeConfig.GetValue("minSizeScale"));
                    newConfig.maxSize = (NodeConfig.GetValue("maxSizeScale") == null) ? newConfig.maxSize : newConfig.maxSize * float.Parse(NodeConfig.GetValue("maxSizeScale"));
                    newConfig.maxParticleSize = (NodeConfig.GetValue("maxParticleSizeScale") == null) ? newConfig.maxParticleSize : newConfig.maxParticleSize * float.Parse(NodeConfig.GetValue("maxParticleSizeScale"));
                    newConfig.minEnergy = (NodeConfig.GetValue("minEnergyScale") == null) ? newConfig.minEnergy : newConfig.minEnergy * float.Parse(NodeConfig.GetValue("minEnergyScale"));
                    newConfig.maxEnergy = (NodeConfig.GetValue("maxEnergyScale") == null) ? newConfig.maxEnergy : newConfig.maxEnergy * float.Parse(NodeConfig.GetValue("maxEnergyScale"));
                    newConfig.minEmission = (NodeConfig.GetValue("minEmissionScale") == null) ? newConfig.minEmission : (int)Math.Round(newConfig.minEmission * float.Parse(NodeConfig.GetValue("minEmissionScale")));
                    newConfig.maxEmission = (NodeConfig.GetValue("maxEmissionScale") == null) ? newConfig.maxEmission : (int)Math.Round(newConfig.maxEmission * float.Parse(NodeConfig.GetValue("maxEmissionScale")));
                    newConfig.sizeGrow = (NodeConfig.GetValue("sizeGrowScale") == null) ? newConfig.sizeGrow : newConfig.sizeGrow * float.Parse(NodeConfig.GetValue("sizeGrowScale"));
                    newConfig.worldVelocity = (NodeConfig.GetValue("worldVelocityScale") == null) ? newConfig.worldVelocity : newConfig.worldVelocity * float.Parse(NodeConfig.GetValue("worldVelocityScale"));
                    newConfig.velocity = (NodeConfig.GetValue("velocityScale") == null) ? newConfig.velocity : newConfig.velocity * float.Parse(NodeConfig.GetValue("velocityScale"));
                    newConfig.rndVelocity = (NodeConfig.GetValue("rndVelocityScale") == null) ? newConfig.rndVelocity : newConfig.rndVelocity * float.Parse(NodeConfig.GetValue("rndVelocityScale"));
                    newConfig.worldForce = (NodeConfig.GetValue("worldForceScale") == null) ? newConfig.worldForce : newConfig.worldForce * float.Parse(NodeConfig.GetValue("worldForceScale"));
                    newConfig.force = (NodeConfig.GetValue("forceScale") == null) ? newConfig.force : newConfig.force * float.Parse(NodeConfig.GetValue("forceScale"));
                    newConfig.rndForce = (NodeConfig.GetValue("rndForceScale") == null) ? newConfig.rndForce : newConfig.rndForce * float.Parse(NodeConfig.GetValue("rndForceScale"));
                    newConfig.changeTime = (NodeConfig.GetValue("changeTimeScale") == null) ? newConfig.changeTime : newConfig.changeTime * float.Parse(NodeConfig.GetValue("changeTimeScale"));

                    if (newConfig.cfgNumber == 0)
                        newFX.newConfig = new Config(newConfig);

                    newFX.configs.Add(newConfig);
                }

                newFX.SortConfigs();
                newFX.SortActions();

                if (int.Parse(node.GetValue("activeGroup")) == 1)
                    effect1.Add(newFX);

                if (int.Parse(node.GetValue("activeGroup")) == 2)
                    effect2.Add(newFX);

                if (int.Parse(node.GetValue("activeGroup")) == 3)
                    effect3.Add(newFX);

                if (int.Parse(node.GetValue("activeGroup")) == 4)
                    effect4.Add(newFX);

                this.Log(debug, "finished startup on " + part.name);
            }

            if (effect1.Count > 0 && effect1[0].configs.Count > 1)
                configchange1 = true;
            if (effect2.Count > 0 && effect2[0].configs.Count > 1)
                configchange2 = true;
            if (effect3.Count > 0 && effect3[0].configs.Count > 1)
                configchange3 = true;
            if (effect4.Count > 0 && effect4[0].configs.Count > 1)
                configchange4 = true;

            UpdateEvents1(effects1Visible, false, (effect1.Count > 0 && effect1[0].controller != null && (effect1[0].controller.startupEffect || effect1[0].controller.shutdownEffect)));
            UpdateEvents2(effects2Visible, false, (effect2.Count > 0 && effect2[0].controller != null && (effect2[0].controller.startupEffect || effect2[0].controller.shutdownEffect)));
            UpdateEvents3(effects3Visible, false, (effect3.Count > 0 && effect3[0].controller != null && (effect3[0].controller.startupEffect || effect3[0].controller.shutdownEffect)));
            UpdateEvents4(effects4Visible, false, (effect4.Count > 0 && effect4[0].controller != null && (effect4[0].controller.startupEffect || effect4[0].controller.shutdownEffect)));

            UpdateEventsTiming(false);

            SetupUI();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            startTime = Planetarium.GetUniversalTime();

            emitter = part.FindModelComponents<KSPParticleEmitter>();
            residualEmitter = part.FindModelComponents<KSPParticleEmitter>();
            if (emitter != null)
                foreach (KSPParticleEmitter unit in emitter)
                    EffectBehaviour.AddParticleEmitter(unit);

            if (HighLogic.LoadedSceneIsEditor)
                if (emitter != null)
                    foreach (KSPParticleEmitter unit in emitter)
                        unit.emit = false;

            Events["NextConfig"].active = false;
            Events["PreviousConfig"].active = false;
            Events["TimeSteamEvent"].active = false;
            Events["UntimeSteamEvent"].active = false;

            if (HighLogic.LoadedSceneIsFlight)
            {
                foreach (KSPParticleEmitter Particle in emitter)
                    Particle.emit = false;

                startEffects = true;
                start = true;
                // This gets this modules configNode so everything else can be set up
                ConfigNode config = this.GetConfig(part.partInfo.partConfig);

                setupConfigNode(config);
            }
            SetupUI();
        }

        public void OnDestroy()
        {
            emitter = part.FindModelComponents<KSPParticleEmitter>();
            if (emitter != null)
                foreach (KSPParticleEmitter unit in emitter)
                    EffectBehaviour.RemoveParticleEmitter(unit);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            savedconfig = node;

            startTime = Planetarium.GetUniversalTime();

            emitter = part.FindModelComponents<KSPParticleEmitter>();
            residualEmitter = part.FindModelComponents<KSPParticleEmitter>();
            if (emitter != null)
                foreach (KSPParticleEmitter unit in emitter)
                    EffectBehaviour.AddParticleEmitter(unit);

            if (HighLogic.LoadedSceneIsEditor)
                if (emitter != null)
                    foreach (KSPParticleEmitter unit in emitter)
                        unit.emit = false;

            Events["NextConfig"].active = false;
            Events["PreviousConfig"].active = false;
            Events["TimeSteamEvent"].active = false;
            Events["UntimeSteamEvent"].active = false;

            if (HighLogic.LoadedSceneIsFlight)
            {
                foreach (KSPParticleEmitter Particle in emitter)
                    Particle.emit = false;

                startEffects = true;
                start = true;

                setupConfigNode(node);
            }
            SetupUI();
        }

        public override void OnAwake()
        {
            base.OnAwake();
        }


        public void OnGUI()
        {
            if (Ui)
            {
                if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                    UiRect = GUI.Window(91, UiRect, OnUi, "Edit Effects");
            }
        }
        private void OnUi(int windowId)
        {
            if (UiConfig == null)
                UiConfig = effect1[0].configs[effect1[0].activeConfig];


            GUILayout.BeginVertical();

            if (GUILayout.Button("x")) Ui = !Ui;
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("active Effect: ");
            int.TryParse(GUILayout.TextField(uiActive.ToString(), GUILayout.Width(50f)), out uiActive);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("changeAtStart: ");
            bool.TryParse(GUILayout.TextField(UiConfig.changeAtStart.ToString(), GUILayout.Width(50f)), out UiConfig.changeAtStart);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("useWorldSpace: ");
            bool.TryParse(GUILayout.TextField(UiConfig.useWorldSpace.ToString(), GUILayout.Width(50f)), out UiConfig.useWorldSpace);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("shape1D: ");
            float.TryParse(GUILayout.TextField(UiConfig.shape1D.ToString(), GUILayout.Width(50f)), out UiConfig.shape1D);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("minSize: ");
            float.TryParse(GUILayout.TextField(UiConfig.minSize.ToString(), GUILayout.Width(50f)), out UiConfig.minSize);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("maxSize: ");
            float.TryParse(GUILayout.TextField(UiConfig.maxSize.ToString(), GUILayout.Width(50f)), out UiConfig.maxSize);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("maxParticleSize: ");
            float.TryParse(GUILayout.TextField(UiConfig.maxParticleSize.ToString(), GUILayout.Width(50f)), out UiConfig.maxParticleSize);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("minEnergy: ");
            float.TryParse(GUILayout.TextField(UiConfig.minEnergy.ToString(), GUILayout.Width(50f)), out UiConfig.minEnergy);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("maxEnergy: ");
            float.TryParse(GUILayout.TextField(UiConfig.maxEnergy.ToString(), GUILayout.Width(50f)), out UiConfig.maxEnergy);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("minEmission: ");
            int.TryParse(GUILayout.TextField(UiConfig.minEmission.ToString(), GUILayout.Width(50f)), out UiConfig.minEmission);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("maxEmission: ");
            int.TryParse(GUILayout.TextField(UiConfig.maxEmission.ToString(), GUILayout.Width(50f)), out UiConfig.maxEmission);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("sizeGrow: ");
            float.TryParse(GUILayout.TextField(UiConfig.sizeGrow.ToString(), GUILayout.Width(50f)), out UiConfig.sizeGrow);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("worldVelocity: ");
            float.TryParse(GUILayout.TextField(UiConfig.worldVelocity.x.ToString(), GUILayout.Width(20f)), out UiConfig.worldVelocity.x);
            float.TryParse(GUILayout.TextField(UiConfig.worldVelocity.y.ToString(), GUILayout.Width(20f)), out UiConfig.worldVelocity.y);
            float.TryParse(GUILayout.TextField(UiConfig.worldVelocity.z.ToString(), GUILayout.Width(20f)), out UiConfig.worldVelocity.z);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("velocity: ");
            float.TryParse(GUILayout.TextField(UiConfig.velocity.x.ToString(), GUILayout.Width(20f)), out UiConfig.velocity.x);
            float.TryParse(GUILayout.TextField(UiConfig.velocity.y.ToString(), GUILayout.Width(20f)), out UiConfig.velocity.y);
            float.TryParse(GUILayout.TextField(UiConfig.velocity.z.ToString(), GUILayout.Width(20f)), out UiConfig.velocity.z);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("rndVelocity: ");
            float.TryParse(GUILayout.TextField(UiConfig.rndVelocity.x.ToString(), GUILayout.Width(20f)), out UiConfig.rndVelocity.x);
            float.TryParse(GUILayout.TextField(UiConfig.rndVelocity.y.ToString(), GUILayout.Width(20f)), out UiConfig.rndVelocity.y);
            float.TryParse(GUILayout.TextField(UiConfig.rndVelocity.z.ToString(), GUILayout.Width(20f)), out UiConfig.rndVelocity.z);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("worldForce: ");
            float.TryParse(GUILayout.TextField(UiConfig.worldForce.x.ToString(), GUILayout.Width(20f)), out UiConfig.worldForce.x);
            float.TryParse(GUILayout.TextField(UiConfig.worldForce.y.ToString(), GUILayout.Width(20f)), out UiConfig.worldForce.y);
            float.TryParse(GUILayout.TextField(UiConfig.worldForce.z.ToString(), GUILayout.Width(20f)), out UiConfig.worldForce.z);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("force: ");
            float.TryParse(GUILayout.TextField(UiConfig.force.x.ToString(), GUILayout.Width(20f)), out UiConfig.force.x);
            float.TryParse(GUILayout.TextField(UiConfig.force.y.ToString(), GUILayout.Width(20f)), out UiConfig.force.y);
            float.TryParse(GUILayout.TextField(UiConfig.force.z.ToString(), GUILayout.Width(20f)), out UiConfig.force.z);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("rndForce: ");
            float.TryParse(GUILayout.TextField(UiConfig.rndForce.x.ToString(), GUILayout.Width(20f)), out UiConfig.rndForce.x);
            float.TryParse(GUILayout.TextField(UiConfig.rndForce.y.ToString(), GUILayout.Width(20f)), out UiConfig.rndForce.y);
            float.TryParse(GUILayout.TextField(UiConfig.rndForce.z.ToString(), GUILayout.Width(20f)), out UiConfig.rndForce.z);
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("changeTime: ");
            double.TryParse(GUILayout.TextField(UiConfig.changeTime.ToString(), GUILayout.Width(50f)), out UiConfig.changeTime);
        }
    }
}
