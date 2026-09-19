
#undef Timed

using PEKKAUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static UrlDir;
using static VehiclePhysics.VPPerformanceDisplay;

namespace PEKKA
{
    public class EngineCFG
    {
        public string engineConfig = "";

        public string thrustTransform = "";

        public bool ullage = false;
        public bool pressureFed = false;

        public float maxThrust = 0;
        public float minThrust = 0;

        public int ignitions = 0;
        public float throttleResponseRate = 0;
        public float throttleStartedMult = 0;
        public float throttleStartupMult = 0;

        public Curve ISPcurve = new Curve();
        public Dictionary<string, float> propellants = new Dictionary<string, float>();
        public Dictionary<string, float> ignitRes = new Dictionary<string, float>();

        public ConfigNode CFG;

        public EngineCFG()
        {
            ignitions = 10000;
            ISPcurve.AddPoint("0 1");
            ISPcurve.AddPoint("1 1");
        }
        public EngineCFG(ConfigNode _CFG, float _symmetry = 1)
        {
            engineConfig = (_CFG.GetValue("name") == null) ? "" : _CFG.GetValue("name");

            thrustTransform = (_CFG.GetValue("thrustVectorTransformName") == null) ? "" : _CFG.GetValue("thrustVectorTransformName");

            ullage = (_CFG.GetValue("ullage") == null) ? false : bool.Parse(_CFG.GetValue("ullage"));
            pressureFed = (_CFG.GetValue("pressureFed") == null) ? false : bool.Parse(_CFG.GetValue("pressureFed"));

            ignitions = (_CFG.GetValue("ignitions") == null) ? 0 : int.Parse(_CFG.GetValue("ignitions"));
            throttleResponseRate = (_CFG.GetValue("throttleResponseRate") == null) ? 0 : float.Parse(_CFG.GetValue("throttleResponseRate"));
            throttleStartedMult = (_CFG.GetValue("throttleStartedMult") == null) ? 0 : float.Parse(_CFG.GetValue("throttleStartedMult"));
            throttleStartupMult = (_CFG.GetValue("throttleStartupMult") == null) ? 0 : float.Parse(_CFG.GetValue("throttleStartupMult"));
            maxThrust = (_CFG.GetValue("maxThrust") == null) ? 0 : float.Parse(_CFG.GetValue("maxThrust")) * _symmetry;
            minThrust = (_CFG.GetValue("minThrust") == null) ? 0 : float.Parse(_CFG.GetValue("minThrust")) * _symmetry;

            foreach (string point in _CFG.GetNode("atmosphereCurve").GetValues("key"))
                ISPcurve.AddPoint(point);

            foreach (ConfigNode props in _CFG.GetNodes("PROPELLANT"))
                propellants.Add(props.GetValue("name"), float.Parse(props.GetValue("ratio")));

            foreach (ConfigNode ignit in _CFG.GetNodes("IGNITOR_RESOURCE"))
                ignitRes.Add(ignit.GetValue("name"), float.Parse(ignit.GetValue("amount")));

            CFG = PopulateConfig();

        }

        public void ChangeSymmetry(float _oldSymmetry, float _newSymmetry)
        {
            if (_oldSymmetry < 0.01)
                _oldSymmetry = 0.01f;
            if (_newSymmetry < 0.01)
                _newSymmetry = 0.01f;
            float symmetryChange = _newSymmetry / _oldSymmetry;

            maxThrust *= symmetryChange;
            minThrust *= symmetryChange;

            CFG = PopulateConfig();
        }

        public void CompleteCFG(EngineCFG _cfg)
        {
            if (engineConfig == "") engineConfig = _cfg.engineConfig;

            if (thrustTransform == "") thrustTransform = _cfg.thrustTransform;

            if (ullage == false) ullage = _cfg.ullage;
            if (pressureFed == false) pressureFed = _cfg.pressureFed;

            if (ignitions == 0) ignitions = _cfg.ignitions;
            if (throttleResponseRate == 0) throttleResponseRate = _cfg.throttleResponseRate;
            if (throttleStartedMult == 0) throttleStartedMult = _cfg.throttleStartedMult;
            if (throttleStartupMult == 0) throttleStartupMult = _cfg.throttleStartupMult;

            if (maxThrust == 0) maxThrust = _cfg.maxThrust;
            if (minThrust == 0) minThrust = _cfg.minThrust;

            if (ISPcurve.points.Count == 0) ISPcurve = _cfg.ISPcurve;
            if (propellants.Count == 0) propellants = _cfg.propellants;
            if (ignitRes.Count == 0) ignitRes = _cfg.ignitRes;

            CFG = PopulateConfig();
        }

        public void AddCFG(EngineCFG _cfg)
        {
            float oldflow = maxThrust / ISPcurve.GetValue(0);
            float newflow = _cfg.maxThrust / _cfg.ISPcurve.GetValue(0);

            engineConfig = "";
            thrustTransform = _cfg.thrustTransform;
            throttleResponseRate = (throttleResponseRate > _cfg.throttleResponseRate) ? throttleResponseRate : _cfg.throttleResponseRate;
            throttleStartedMult = (throttleStartedMult > _cfg.throttleStartedMult) ? throttleStartedMult : _cfg.throttleStartedMult;
            throttleStartupMult = (throttleStartupMult > _cfg.throttleStartupMult) ? throttleStartupMult : _cfg.throttleStartupMult;
            ignitions = (ignitions != 0 && ignitions < _cfg.ignitions) ? ignitions : _cfg.ignitions;
            ullage = ullage || _cfg.ullage;
            pressureFed = pressureFed || _cfg.pressureFed;

            foreach (Point point in ISPcurve.points)
                point.y = (oldflow* point.y + newflow*_cfg.ISPcurve.GetValue(point.x)) / (oldflow+newflow);

            List<string> propNames = new List<string>();
            foreach (KeyValuePair<string, float> kvp in propellants)
                propNames.Add(kvp.Key);
            foreach (KeyValuePair<string, float> kvp in _cfg.propellants)
                if (!propNames.Contains(kvp.Key))
                    propNames.Add(kvp.Key);

            foreach (string propName in propNames)
                propellants[propName] = ((propellants.ContainsKey(propName)? propellants[propName]*oldflow: 0) + (_cfg.propellants.ContainsKey(propName)? _cfg.propellants[propName]*newflow: 0)) / (oldflow + newflow);

            List<string> ignitNames = new List<string>();
            foreach (KeyValuePair<string, float> kvp in ignitRes)
                ignitNames.Add(kvp.Key);
            foreach (KeyValuePair<string, float> kvp in _cfg.ignitRes)
                if (!ignitNames.Contains(kvp.Key))
                    ignitNames.Add(kvp.Key);

            foreach (string ignitName in ignitNames)
                ignitRes[ignitName] = (ignitRes.ContainsKey(ignitName) ? ignitRes[ignitName] : 0) + (_cfg.ignitRes.ContainsKey(ignitName) ? _cfg.ignitRes[ignitName] : 0);

            maxThrust += _cfg.maxThrust;
            minThrust += _cfg.minThrust;

            CFG = PopulateConfig();
        }

        public ConfigNode PopulateConfig()
        {
            if (false)
            {
                //config.ClearNodes();
                //
                //config.SetValue("thrustVectorTransformName", thrustTransform);
                //config.SetValue("maxThrust", maxThrust);
                //config.SetValue("minThrust", minThrust);
                //foreach (KeyValuePair<string, float> kvp in propellants)
                //{
                //    ConfigNode prop = config.AddNode("PROPELLANT");
                //    prop.AddValue("name", kvp.Key);
                //    prop.AddValue("ratio", kvp.Value);
                //}
                //foreach (KeyValuePair<string, float> kvp in ignitRes)
                //{
                //    ConfigNode prop = config.AddNode("IGNITOR_RESOURCE");
                //    prop.AddValue("name", kvp.Key);
                //    prop.AddValue("amount", kvp.Value);
                //}
                //ConfigNode atm = config.AddNode("atmosphereCurve");
                //foreach (Point point in ISPcurve.points)
                //    atm.AddNode("key", (point.x + " " + point.y));
                //
                //config.GetNode("PROPELLANT").AddValue("DrawGauge", "True");
                //
                //return config;
            }
            else
            {
                ConfigNode output = new ConfigNode("MODULE");

                output.AddValue("thrustVectorTransformName", thrustTransform);
                if (throttleResponseRate != 0) output.AddValue("throttleResponseRate", throttleResponseRate.ToString());
                if (throttleStartedMult != 0) output.AddValue("throttleStartedMult", throttleStartedMult.ToString());
                if (throttleStartupMult != 0) output.AddValue("throttleStartupMult", throttleStartupMult.ToString());
                output.AddValue("ullage", ullage.ToString());
                output.AddValue("pressureFed", pressureFed.ToString());
                output.AddValue("ignitions", ignitions.ToString());
                output.AddValue("maxThrust", maxThrust.ToString());
                output.AddValue("minThrust", minThrust.ToString());
                foreach (KeyValuePair<string, float> kvp in propellants)
                {
                    ConfigNode prop = output.AddNode("PROPELLANT");
                    prop.AddValue("name", kvp.Key);
                    prop.AddValue("ratio", kvp.Value.ToString());
                }
                foreach (KeyValuePair<string, float> kvp in ignitRes)
                {
                    ConfigNode prop = output.AddNode("IGNITOR_RESOURCE");
                    prop.AddValue("name", kvp.Key);
                    prop.AddValue("amount", kvp.Value.ToString());
                }
                ConfigNode atm = output.AddNode("atmosphereCurve");
                foreach (Point point in ISPcurve.points)
                    atm.AddValue("key", (point.x.ToString() + " " + point.y.ToString()));

                output.GetNode("PROPELLANT").AddValue("DrawGauge", "True");

                return output;
            }
        }
    }

    public class ExtraEngine
    {
        List<GameObject> engines = new List<GameObject>();
        public List<int> removeIndex = null;
        public List<int> disableIndex = null;

        public string name = "";
        public float mass = 0;
        public EngineCFG cfg;

        public Vector3 position = new Vector3();
        public Vector3 rotation = new Vector3();
        public Vector3 scale = new Vector3();

        public int symmetry = 1;
        public string axis = "";

        public bool isEditor;
        public bool autoShutDown = true;

        public double failure = 0;
        public double ignitfailure = 0;

        public ExtraEngine(bool _isEditor, string _name, float _mass, Vector3 _position, Vector3 _rotation, Vector3 _scale, int _symmetry, string _axis, GameObject _engineModel, Part _part, EngineCFG _config, List<int> _removeIndex, List<int> _disableIndex)
        {
            name = _name;
            position = _position;
            rotation = _rotation;
            scale = _scale;

            removeIndex = _removeIndex;
            if (removeIndex.Count > 0)
                removeIndex.Sort();
            disableIndex = _disableIndex;
            if (disableIndex.Count > 0)
                disableIndex.Sort();

            symmetry = _symmetry;
            axis = _axis;

            isEditor = _isEditor;

            int n = symmetry;
            if (axis != "")
            {
                n = 2;
                symmetry = 2;
            }

            ResizeEngines(engines.Count, n, _engineModel, _part);
            TransformEngines(_part);

            n -= removeIndex.Count;

            mass = n * _mass;

            n -= autoShutDown ? disableIndex.Count : 0;

            cfg = _config;
            cfg.ChangeSymmetry(1, n);

            foreach (int i in removeIndex)
                hideEngine(i, true);
        }

        public float Edit(float _mass, Vector3 _position, Vector3 _rotation, Vector3 _scale, int _symmetry, string _axis, GameObject _engineModel, Part _part, ConfigNode _config)
        {
            int oldn = symmetry;
            if (axis != "")
            {
                oldn = 2;
            }
            oldn -= removeIndex.Count;
            oldn -= autoShutDown ? disableIndex.Count : 0;

            position = _position;
            rotation = _rotation;
            scale = _scale;

            symmetry = _symmetry;
            axis = _axis;

            int n = symmetry;
            if (axis != "")
            {
                n = 2;
                symmetry = 2;
            }

            ResizeEngines(engines.Count, n, _engineModel, _part);
            TransformEngines(_part);

            n -= removeIndex.Count;

            float output = n * mass - mass;
            mass = n * _mass;

            n -= autoShutDown ? disableIndex.Count : 0;
            cfg.ChangeSymmetry(oldn, n);

            foreach (int i in removeIndex)
                output -= hideEngine(i, true);

            return output;
        }

        public void ResizeEngines(int _start, int _n, GameObject _engineModel, Part _part)
        {
            while (_start < _n)
            {
                GameObject engine = GameObject.Instantiate(_engineModel);
                engine.transform.SetParent(_part.transform.FindChild("model"));
                engine.SetActive(true);

                engines.Add(engine);
                _start++;
            }
            while (_start > _n)
            {
                hideEngine(_start - 1);
                _start--;
            }
            _engineModel.DestroyGameObject();
        }

        public void TransformEngines(Part _part)
        {
            if (axis != "")
            {
                engines[0].transform.localPosition = position;
                engines[0].transform.localEulerAngles = rotation;
                engines[0].transform.localScale = scale;

                engines[1].transform.localScale = scale;
                if (axis == "x")
                {
                    engines[1].transform.localPosition = new Vector3(-position.x, position.y, position.z);
                    engines[1].transform.localEulerAngles = new Vector3(rotation.x, 180 - rotation.y, rotation.z);
                }
                else
                {
                    engines[1].transform.localPosition = new Vector3(position.x, position.y, -position.z);
                    engines[1].transform.localEulerAngles = new Vector3(rotation.x, -rotation.y, rotation.z);
                }
                return;
            }
            for (int i = 0; i < symmetry; i++)
            {
                //while (removeIndex.Contains(i))
                //    i++;
                //if (i >= symmetry)
                //    break;

                engines[i].transform.localPosition = position;
                engines[i].transform.localEulerAngles = rotation;
                engines[i].transform.localScale = scale;

                engines[i].transform.RotateAround(_part.transform.FindChild("model").position, _part.transform.FindChild("model").up, i * 360 / symmetry);
            }
        }

        public void CheckEngines(Part _part)
        {
            List<Vector3> oldtransforms = new List<Vector3>();
            foreach (GameObject eng in engines)
            {
                Vector3 v = new Vector3(eng.transform.localPosition.x, eng.transform.localPosition.y, eng.transform.localPosition.z);
                oldtransforms.Add(eng.transform.localPosition);
            }
            TransformEngines(_part);
            for (int i = 0; i < engines.Count; i++)
            {
                if (oldtransforms[i] - engines[i].transform.localPosition != Vector3.zero)
                    this.Log(true, $"Moved engine {i} by {(oldtransforms[i] - engines[i].transform.localPosition).magnitude}");
            }
        }

        public void CleanThrustTransforms()
        {
            this.Log(true, $"disableIndex {String.Join(", ", disableIndex)}");
            this.Log(true, $"removeIndex {String.Join(", ", removeIndex)}");
            for (int n = 0; n < engines.Count(); n++)
            {
                while (autoShutDown && disableIndex.Contains(n))
                    n++;
                this.Log(true, $"{n}");
                if (n >= engines.Count())
                    return;
                Transform thrustTransform = Utils.FindChild(engines[n].transform, cfg.thrustTransform);
                this.Log(true, $"thrustTransform {n} == null {thrustTransform == null}");
                for (int i = 0; i < thrustTransform.childCount; i++)
                    thrustTransform.GetChild(i).gameObject.DestroyGameObject();
            }
        }

        public void ChangeThrustTransforms(string _newName, string _oldName)
        {
            if (_oldName == "") _oldName = cfg.thrustTransform;
            for (int n = 0; n < engines.Count(); n++)
            {
                while (autoShutDown && disableIndex.Contains(n))
                    n++;
                Transform thrustTransform = Utils.FindChild(engines[n].transform, _oldName);
                thrustTransform.name = _newName;
            }
            cfg.thrustTransform = _newName;
            cfg.CFG = cfg.PopulateConfig();
        }

        public ConfigNode ExportNode()
        {
            ConfigNode export = new ConfigNode("ENGINES");
            export.AddValue("Name", name);
            export.AddValue("ConfigName", cfg.engineConfig);
            export.AddValue("Position", ConfigNode.WriteVector(position));
            export.AddValue("Rotation", ConfigNode.WriteVector(rotation));
            export.AddValue("Symmetry", symmetry);
            export.AddValue("Axis", axis);
            export.AddValue("RemoveIndex", String.Join(", ", removeIndex));
            export.AddValue("DisableIndex", String.Join(", ", disableIndex));

            return export;
        }

        public ConfigNode GetLookAtConstraints(string _rotatorsName, string _targetName, Vector3 _rotationAxis, string _index)
        {
            ConfigNode output = new ConfigNode();
            for (int i = 0; i < engines.Count; i++)
            {
                ConfigNode tmp = new ConfigNode("CONSTRAINLOOKFX");

                Transform target = Utils.FindChild(engines[i].transform, _targetName);
                List<Transform> rotators = Utils.FindChildren(engines[i].transform, _rotatorsName, new List<Transform>());

                if (rotators.Count > 0);
                    foreach (Transform rotator in rotators)
                    rotator.name = $"{_rotatorsName}-{_index}-{i}";

                if (target != null)
                    target.name = $"{_targetName}-{_index}-{i}";

                tmp.AddValue("targetName", $"{_targetName}-{_index}-{i}");
                tmp.AddValue("rotatorsName", $"{_rotatorsName}-{_index}-{i}");
                output.AddNode(tmp);
            }
            return output;
        }

        public Vector2 GetPosition(int _Index)
        {
            Vector2 output;
            if (axis != "")
            {
                if (axis == "x")
                    output = new Vector2((_Index == 0) ? position.x : -position.x, position.z);
                else
                    output = new Vector2(position.x, (_Index == 0) ? position.z : -position.z);
                return output;
            }
            float a = (float)Math.Atan2(position.x, position.z) + (float)(Math.PI * 2 * _Index / symmetry);
            Vector2 len = new Vector2(position.x, position.z);
            output = new Vector2(len.magnitude * (float)Math.Sin(a), len.magnitude * (float)Math.Cos(a));
            return output;
        }

        public float showEngine(int _Index)
        {
            float output = 0;
            if (removeIndex.Contains(_Index))
            {
                cfg.ChangeSymmetry((symmetry-removeIndex.Count - (autoShutDown ? disableIndex.Count : 0)), (symmetry-removeIndex.Count - (autoShutDown ? disableIndex.Count : 0) + 1));
                output = mass / symmetry;
                mass *= (symmetry-removeIndex.Count+1) / (symmetry-removeIndex.Count);

                engines[_Index].SetActive(true);
                removeIndex.Remove(_Index);
            }

            return output;
        }

        public float hideEngine(int _Index, bool _hideAll = false)
        {
            float output = 0;
            if (_hideAll) engines[_Index].SetActive(false);
            else if (!removeIndex.Contains(_Index))
            {
                if (autoShutDown && disableIndex.Contains(_Index))
                {
                    Transform thrustTransform = Utils.FindChild(engines[_Index].transform, $"Not {cfg.thrustTransform}");
                    thrustTransform.name = cfg.thrustTransform;
                    disableIndex.Remove(_Index);
                }
                else
                    cfg.ChangeSymmetry(symmetry - removeIndex.Count - (autoShutDown ? disableIndex.Count : 0), symmetry - removeIndex.Count - (autoShutDown ? disableIndex.Count : 0) - 1);
                output = mass / (symmetry - removeIndex.Count);
                mass *= (symmetry-removeIndex.Count-1) / (symmetry-removeIndex.Count);

                engines[_Index].SetActive(false);
                removeIndex.Add(_Index);
                removeIndex.Sort();
            }

            return output;
        }

        public void enableAutoShutDown()
        {
            foreach (int n in disableIndex)
            {
                Transform thrustTransform = Utils.FindChild(engines[n].transform, cfg.thrustTransform);
                thrustTransform.name = $"Not {cfg.thrustTransform}";
            }
            cfg.ChangeSymmetry((symmetry - removeIndex.Count), (symmetry - removeIndex.Count - disableIndex.Count));
            autoShutDown = true;
        }

        public void disableAutoShutDown()
        {
            foreach (int n in disableIndex)
            {
                Transform thrustTransform = Utils.FindChild(engines[n].transform, $"Not {cfg.thrustTransform}");
                thrustTransform.name = cfg.thrustTransform;
            }
            cfg.ChangeSymmetry((symmetry - removeIndex.Count - disableIndex.Count), (symmetry - removeIndex.Count));
            autoShutDown = false;
        }

        public void disableEngine(int _Index)
        {
            if (!autoShutDown && !disableIndex.Contains(_Index))
            {
                disableIndex.Add(_Index);
                disableIndex.Sort();
            }

            else if (!disableIndex.Contains(_Index))
            {
                cfg.ChangeSymmetry((symmetry - removeIndex.Count - (autoShutDown ? disableIndex.Count : 0)), (symmetry - removeIndex.Count - (autoShutDown ? disableIndex.Count : 0) - 1));

                Transform thrustTransform = Utils.FindChild(engines[_Index].transform, cfg.thrustTransform);
                for (int i = 0; i < thrustTransform.childCount; i++)
                    thrustTransform.GetChild(i).gameObject.DestroyGameObject();

                thrustTransform.name = $"Not {cfg.thrustTransform}";

                disableIndex.Add(_Index);
                disableIndex.Sort();
            }
        }
    }

    public class EngineGroup
    {
        public List<ExtraEngine> engines = new List<ExtraEngine>();
        public EngineCFG groupConfig = new EngineCFG();
        public string groupName = "";
        public float mass = 0;
        public bool usePrimaryPlume = false;
        public bool enableClusterPlume = false;

        public ModuleEngines engineModule;
        public PartModule primaryWaterfallModule;
        public PartModule clusterWaterfallModule;
        public PartModule secondaryWaterfallModule;
        public ModuleGimbal gimbalModule;

        public ConfigNode primaryWaterfallConfig = null;
        public ConfigNode clusterWaterfallConfig = null;
        public ConfigNode secondaryWaterfallConfig = null;
        public ConfigNode gimbalConfig = null;

        public bool hasFailure = false;
        public float oldThrottle = 0;

        public EngineGroup(string _groupName, bool _usePrimaryPlume, bool _enableClusterPlume, ModuleEngines _engineModule, ModuleGimbal _gimbalModule = null, ConfigNode _primaryWaterfallConfig = null, ConfigNode _clusterWaterfallConfig = null)
        {
            groupName = _groupName;

            usePrimaryPlume = _usePrimaryPlume;
            enableClusterPlume = _enableClusterPlume;

            engineModule = _engineModule;
            gimbalModule = _gimbalModule;

            primaryWaterfallConfig = _primaryWaterfallConfig;
            clusterWaterfallConfig = _clusterWaterfallConfig;
        }

        public void AddEngine(bool _isEditor, string _name, float _mass, Vector3 _position, Vector3 _rotation, Vector3 _scale, int _symmetry, string _axis, GameObject _engineModel, Part _part, EngineCFG _config, List<int> _removeIndex, List<int> _disableIndex)
        {
            if (_removeIndex == null)
                _removeIndex = new List<int>();
            engines.Add(new ExtraEngine(_isEditor, _name, _mass, _position, _rotation, _scale, _symmetry, _axis, _engineModel, _part, _config, _removeIndex, _disableIndex));
            mass += engines[engines.Count - 1].mass;
            groupConfig.AddCFG(engines[engines.Count - 1].cfg);
        }

        public void Edit(int _index, float _mass, Vector3 _position, Vector3 _rotation, Vector3 _scale, int _symmetry, string _axis, GameObject _engineModel, Part _part, ConfigNode _config)
        {
            mass += engines[_index].Edit(_mass, _position, _rotation, _scale, _symmetry, _axis, _engineModel, _part, _config);
        }

        public void CleanThrustTransforms()
        {
            foreach (ExtraEngine engine in engines)
                engine.CleanThrustTransforms();
        }
        public void ChangeThrustTransforms(string _newName, string _oldName = "")
        {
            foreach (ExtraEngine engine in engines)
                engine.ChangeThrustTransforms(_newName, _oldName);
            groupConfig.thrustTransform = _newName;
            groupConfig.PopulateConfig();
        }

        public ConfigNode ExportGroupNode()
        {
            ConfigNode export = new ConfigNode("GROUP");
            export.AddValue("name", groupName);

            if (primaryWaterfallConfig != null)
                export.AddNode(primaryWaterfallConfig);
            if (clusterWaterfallConfig != null)
                export.AddNode(clusterWaterfallConfig);

            foreach (ExtraEngine engine in engines)
                export.AddNode(engine.ExportNode());

            return export;
        }

        public string ExportGroup()
        {
            return ExportGroupNode().ToString().Replace("\n", "\n\t\t");
        }

        public ConfigNode GetLookAtConstraints(string _rotatorsName, string _targetName, Vector3 _rotationAxis, int _index)
        {
            ConfigNode output = new ConfigNode();
            for (int i = 0; i < engines.Count; i++)
            {
                ConfigNode tmp = engines[i].GetLookAtConstraints(_rotatorsName, _targetName, _rotationAxis, $"{_index}-{i}");
                foreach (ConfigNode tmpNode in tmp.nodes)
                    output.AddNode(tmpNode);
            }
            return output;
        }

        public Vector2 GetPosition(int _Index, int _engineIndex)
        {
            return engines[_engineIndex].GetPosition(_Index);
        }

        public void showEngine(int _Index, int _engineIndex)
        {
            mass += engines[_engineIndex].showEngine(_Index);
            groupConfig = new EngineCFG();
            foreach (ExtraEngine e in engines)
                groupConfig.AddCFG(e.cfg);
        }

        public void hideEngine(int _Index, int _engineIndex)
        {
            mass -= engines[_engineIndex].hideEngine(_Index);
            groupConfig = new EngineCFG();
            foreach (ExtraEngine e in engines)
                groupConfig.AddCFG(e.cfg);
        }

        public void deleteEngine(int _engineIndex)
        {
            for (int i = 0; i < engines[_engineIndex].symmetry; i++)
                mass -= engines[_engineIndex].hideEngine(i);
            engines.RemoveAt(_engineIndex);
            groupConfig = new EngineCFG();
            foreach (ExtraEngine e in engines)
                groupConfig.AddCFG(e.cfg);
        }

        public void disableEngine(int _Index, int _engineIndex)
        {
            engines[_engineIndex].disableEngine(_Index);
            groupConfig = new EngineCFG();
            foreach (ExtraEngine e in engines)
                groupConfig.AddCFG(e.cfg);
        }

        public void enableAutoShutDown()
        {
            foreach (ExtraEngine e in engines)
                e.enableAutoShutDown();
            groupConfig = new EngineCFG();
            foreach (ExtraEngine e in engines)
                groupConfig.AddCFG(e.cfg);
        }

        public void disableAutoShutDown()
        {
            foreach (ExtraEngine e in engines)
                e.disableAutoShutDown();
            groupConfig = new EngineCFG();
            foreach (ExtraEngine e in engines)
                groupConfig.AddCFG(e.cfg);
        }
    }

    public class ModuleEngineWeld : PartModule
    {
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool usePrimaryPlume = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public bool enableClusterPlume = false;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public double autoshutdownbenefit = 5;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public double shutdownexplosion = 10;
        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public double shutdownallexplosion = 100;

        [KSPField(guiActiveEditor = false, isPersistant = false)]
        public string addModel = "";

        public string TAG = "[ModuleEngineWeld]";

        public const string groupName = "ModuleEngineWeld";

        private Rect engineWindowPosition = new Rect(100, 100, 900, 800);
        private Vector2 enginePosition = new Vector2(0, 0);
        private Vector2 groupPosition = new Vector2(0, 0);
        private Rect editWindowPosition = new Rect(100, 100, 200, 200);
        private List<bool> isHidingEngine = new List<bool>();
        private float radius = 0.1f;

        public List<string> urls = new List<string>();
        public List<string> names = new List<string>();
        public List<Texture2D> thumbnails = new List<Texture2D>();

        public ModuleEngines test;

        public List<EngineGroup> groups = new List<EngineGroup>();
        public EngineCFG activeEngine;
        public EngineCFG activeConfig;
        public EngineCFG engineCfg;

        float mass = 0;
        bool showUI = false;
        List<string> engineNames = new List<string>();
        ConfigNode engineConfig;
        ConfigNode templateConfig;
        ConfigNode savedConfig;
        ConfigNode lookatconstraint;
        ConfigNode primaryWaterfallConfig = null;
        ConfigNode clusterWaterfallConfig = null;
        ConfigNode effectConfig = null;

        int fixedUpdates = 0;

        bool loaded = false;
        bool enginesSelected = false;
        int selectedGroup = -1;
        int selectedEngines = -1;
        int selectedEngine = -1;
        string engineName = "";
        string currentCfg = "";

        string pX = "0";
        string pY = "0";
        string pZ = "0";
        string rX = "0";
        string rY = "0";
        string rZ = "0";
        string symmetry = "1";
        string axis = "";

        bool editing = false;

        public System.Random failureGenerator;
        public double checkedFailure = 0;

#if Timed
        PerfTimer timer = new PerfTimer();
#endif

        string[] addModules = { "ModulePekkaVent", "ModulePekkaAnim", "FXModuleLookAtConstraint" };// "ModuleEnginesFX", "ModuleEnginesRF", "ModuleGimbal", 
        string[] removeModules = { "ModuleWaterfallFX" };
        string[] updateRemoveModules = {"FARAeroPartModule", "FARPartModule", "GeometryPartModule" };

        [KSPEvent(guiActive = true, guiName = "Switch Plumes", active = true, groupName = groupName, groupDisplayName = groupName)]
        public void SwitchPlumes()
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            foreach (EngineGroup group in groups)
            {
                group.usePrimaryPlume = !group.usePrimaryPlume;

                addWaterfall(group, sta, true, true);
            }
            if (groups[0].usePrimaryPlume && groups[0].clusterWaterfallConfig != null)
                Events["ToggleClusterPlume"].active = true;
            else
                Events["ToggleClusterPlume"].active = false;
        }

        [KSPEvent(guiActive = true, guiName = "Toggle Cluster Plume", active = true, groupName = groupName, groupDisplayName = groupName)]
        public void ToggleClusterPlume()
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            foreach (EngineGroup group in groups)
            {
                group.enableClusterPlume = !group.enableClusterPlume;

                addWaterfall(group, sta, false, true);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Enable auto Shutdown", active = false, groupName = groupName, groupDisplayName = groupName)]
        public void EnableAutoShutDown()
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            for (int i = 0; i < groups.Count; i++)
            {
                this.Log(true, $"EnableShutdown on {i}");
                groups[i].enableAutoShutDown();

                float oldthrottle = groups[i].engineModule.currentThrottle;
                AddEngineModules(sta, i, false, false, false);
                groups[i].engineModule.currentThrottle = oldthrottle;
            }

            Events["EnableautoShutDown"].active = false;
            Events["DisableAutoShutDown"].active = true;
        }

        [KSPEvent(guiActive = true, guiName = "Disable auto Shutdown", active = true, groupName = groupName, groupDisplayName = groupName)]
        public void DisableAutoShutDown()
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            for (int i = 0; i < groups.Count; i++)
            {
                this.Log(true, $"DisableShutdown on {i}");
                groups[i].disableAutoShutDown();

                float oldthrottle = groups[i].engineModule.currentThrottle;
                AddEngineModules(sta, i, false, false, false);
                groups[i].engineModule.currentThrottle = oldthrottle;
            }

            Events["EnableautoShutDown"].active = true;
            Events["DisableAutoShutDown"].active = false;
        }

        [KSPAction(guiName = "Switch Plumes")]
        public void SwitchPlumes(KSPActionParam param)
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            foreach (EngineGroup group in groups)
            {
                group.usePrimaryPlume = !group.usePrimaryPlume;

                addWaterfall(group, sta, true, true);
            }
            if (groups[0].usePrimaryPlume && groups[0].clusterWaterfallConfig != null)
                Events["ToggleClusterPlume"].active = true;
            else
                Events["ToggleClusterPlume"].active = false;
        }

        [KSPAction(guiName = "Toggle Cluster Plume")]
        public void ToggleClusterPlume(KSPActionParam param)
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            foreach (EngineGroup group in groups)
            {
                group.enableClusterPlume = !group.enableClusterPlume;

                addWaterfall(group, sta, false, true);
            }
        }

        [KSPAction(guiName = groupName)]
        public void ToggleAutoShutDown(KSPActionParam param)
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].engines[0].autoShutDown)
                {
                    this.Log(true, $"DisableShutdown on {i}");
                    groups[i].disableAutoShutDown();
                }
                else
                {
                    this.Log(true, $"EnableShutdown on {i}");
                    groups[i].enableAutoShutDown();
                }

                float oldthrottle = groups[i].engineModule.currentThrottle;
                AddEngineModules(sta, i, false, false, false);
                groups[i].engineModule.currentThrottle = oldthrottle;
            }

            Events["EnableautoShutDown"].active = !Events["EnableautoShutDown"].active;
            Events["DisableAutoShutDown"].active = !Events["DisableAutoShutDown"].active;
        }

        public void RemoveEngine(int _engineIndex, int _groupIndex, bool _explosive)
        {
            this.Log(true, $" {_engineIndex} {_groupIndex} {_explosive}");

            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            if (_explosive)
                for (int i = 0; i < groups[_groupIndex].engines[_engineIndex].symmetry; i++)
                    groups[_groupIndex].hideEngine(i, _engineIndex);
            else
                for (int i = 0; i < groups[_groupIndex].engines[_engineIndex].symmetry; i++)
                    groups[_groupIndex].disableEngine(i, _engineIndex);

            float oldthrottle = groups[_groupIndex].engineModule.currentThrottle;
            AddEngineModules(sta, _groupIndex, false, false, false);
            groups[_groupIndex].engineModule.currentThrottle = oldthrottle;

            this.Log(true, groups[_groupIndex].engines[_engineIndex].cfg.CFG.ToString());
        }
        public void RemoveEngine(int _Index, int _engineIndex, int _groupIndex, bool _explosive)
        {
            this.Log(true, $"{_Index} {_engineIndex} {_groupIndex} {_explosive}");

            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;
            if (_explosive)
                groups[_groupIndex].hideEngine(_Index, _engineIndex);
            else
                groups[_groupIndex].disableEngine(_Index, _engineIndex);

            float oldthrottle = groups[_groupIndex].engineModule.currentThrottle;
            AddEngineModules(sta, _groupIndex, false, false, false);
            groups[_groupIndex].engineModule.currentThrottle = oldthrottle;

            this.Log(true, groups[_groupIndex].engines[_engineIndex].cfg.CFG.ToString());
        }

        public void CleanClusterTransforms()
        {
            Transform thrustTransform = Utils.FindChild(part.transform, "thrustTransform");
            for (int i = 0; i < thrustTransform.childCount; i++)
                thrustTransform.GetChild(i).gameObject.DestroyGameObject();
        }

        public ConfigNode createWaterfallNode(EngineGroup _group, ConfigNode node, string _Transform)
        {
            ConfigNode waterfallConfig = new ConfigNode("Module");
            waterfallConfig.AddValue("name", "ModuleWaterfallFX");
            waterfallConfig.AddValue("moduleID", _group.groupName);
            waterfallConfig.AddValue("engineID", "AddEngine");
            foreach (ConfigNode controller in node.GetNodes("CONTROLLER"))
            {
                if (!controller.HasValue("engineID"))
                    waterfallConfig.AddNode(controller.CreateCopy());
                else
                {
                    ConfigNode throttleNode = waterfallConfig.AddNode("THROTTLECONTROLLER");
                    throttleNode.AddValue("name", "throttle");
                    throttleNode.AddValue("engineID", "AddEngine");
                    throttleNode.AddValue("responseRateUp", "1");
                    throttleNode.AddValue("responseRateDown", "1");
                }
            }
            foreach (ConfigNode template in node.GetNodes("TEMPLATE"))
            {
                ConfigNode contnode = waterfallConfig.AddNode(template.CreateCopy());
                if (contnode.HasValue("__rowaterfall"))
                    contnode.RemoveValue("__rowaterfall"); //groups[_index].groupName
                contnode.SetValue("overrideParentTransform", _Transform);
            }
            return waterfallConfig;
        }

        public void addWaterfall(EngineGroup _group, StartState state, bool _updatePrimary, bool _updateCluster)
        {
            if (_group.primaryWaterfallModule != null && _updatePrimary)
            {
                Destroy(_group.primaryWaterfallModule);
                this.Log(true, "Removed primaryWaterfallModule");
            }
            if (_group.clusterWaterfallModule != null && _updateCluster)
            {
                Destroy(_group.clusterWaterfallModule);
                this.Log(true, "Removed clusterWaterfallModule");
            }
            if (_group.secondaryWaterfallModule != null)
            {
                Destroy(_group.secondaryWaterfallModule);
                this.Log(true, "Removed secondaryWaterfallModule");
            }
            _group.CleanThrustTransforms();
            CleanClusterTransforms();

            if (_group.usePrimaryPlume)
            {
                if (true)//(!toggleCluster)
                {
                    PartModule WaterfallModule = _group.primaryWaterfallModule;
                    ConfigNode waterfallConfig = _group.primaryWaterfallConfig;
                    if (waterfallConfig == null && primaryWaterfallConfig != null)
                        waterfallConfig = primaryWaterfallConfig.CreateCopy();
                    if (waterfallConfig != null) waterfallConfig = createWaterfallNode(_group, waterfallConfig, _group.engines[0].cfg.thrustTransform);

                    if (WaterfallModule == null && waterfallConfig != null)
                    {
                        ConfigNode waterNode = new ConfigNode("MODULE");
                        waterNode.AddValue("name", "ModuleWaterfallFX");
                        WaterfallModule = part.AddModule(waterNode.CreateCopy(), false);
                        _group.primaryWaterfallModule = WaterfallModule;
                        this.Log(true, "added primaryWaterfallConfig");
                    }
                    if (waterfallConfig != null)
                    {
                        WaterfallModule.OnLoad(waterfallConfig.CreateCopy());
                        WaterfallModule.OnStart(state);
                        this.Log(true, "loaded primaryWaterfallConfig");
                    }
                }

                if (_group.enableClusterPlume)
                {
                    PartModule WaterfallModule = _group.clusterWaterfallModule;
                    ConfigNode waterfallConfig = _group.clusterWaterfallConfig;
                    this.Log(true, $"{clusterWaterfallConfig != null}");
                    if (waterfallConfig == null && clusterWaterfallConfig != null)
                        waterfallConfig = clusterWaterfallConfig.CreateCopy();
                    this.Log(true, $"{_group.clusterWaterfallConfig != null}");
                    if (waterfallConfig != null) waterfallConfig = createWaterfallNode(_group, _group.clusterWaterfallConfig, "thrustTransform");

                    if (WaterfallModule == null && waterfallConfig != null)
                    {
                        ConfigNode waterNode = new ConfigNode("MODULE");
                        waterNode.AddValue("name", "ModuleWaterfallFX");
                        WaterfallModule = part.AddModule(waterNode.CreateCopy(), false);
                        _group.clusterWaterfallModule = WaterfallModule;
                        this.Log(true, "added clusterWaterfallModule");
                    }
                    if (waterfallConfig != null)
                    {
                        WaterfallModule.OnLoad(waterfallConfig.CreateCopy());
                        WaterfallModule.OnStart(state);
                        this.Log(true, "loaded clusterWaterfallModule");
                    }
                }
            }
            else
            {
                PartModule WaterfallModule = _group.secondaryWaterfallModule;
                ConfigNode waterfallConfig = createWaterfallNode(_group, _group.secondaryWaterfallConfig, _group.engines[0].cfg.thrustTransform);

                if (WaterfallModule == null)
                {
                    ConfigNode waterNode = new ConfigNode("MODULE");
                    waterNode.AddValue("name", "ModuleWaterfallFX");
                    //moduleNode.SetValue("moduleID", "");
                    //foreach (ConfigNode template in moduleNode.GetNodes("TEMPLATE"))
                    //    template.SetValue("overrideParentTransform", engines[_index].engines[0].cfg.thrustTransform);
                    WaterfallModule = part.AddModule(waterNode.CreateCopy(), false);
                    _group.secondaryWaterfallModule = WaterfallModule;
                }
                if (waterfallConfig != null)
                {
                    //print(waterfallConfig.ToString());
                    WaterfallModule.OnLoad(waterfallConfig.CreateCopy());

                    //WaterfallModule.OnStart(StartState.Editor);

                    WaterfallModule.OnStart(state);
                    //part.RemoveModule(module);
                    //part.AddModule(waterfallConfig);
                }
            }
        }

        public void CheckForFailures(int _Index, bool _startUp, double _finalMult)
        {
            checkedFailure = Planetarium.GetUniversalTime();
            for (int n = 0; n < groups[_Index].engines.Count; n++)
                for (int m = 0; m < groups[_Index].engines[n].symmetry; m++)
                {
                    double rn = failureGenerator.NextDouble() / _finalMult;

                    if (!groups[_Index].engines[n].removeIndex.Contains(m) && !groups[_Index].engines[n].disableIndex.Contains(m))
                    {
                        if (_startUp)
                        {
                            if (rn < groups[_Index].engines[n].ignitfailure / (shutdownallexplosion * (groups[_Index].engines[n].autoShutDown ? autoshutdownbenefit : 1)))
                            {
                                this.Log(true, $"exploded all on startup because {rn} was less than {groups[_Index].engines[n].ignitfailure / shutdownallexplosion}");
                                RemoveEngine(n, _Index, true);
                            }
                            else if (rn < groups[_Index].engines[n].ignitfailure / (shutdownexplosion * (groups[_Index].engines[n].autoShutDown ? autoshutdownbenefit : 1)))
                            {
                                this.Log(true, $"exploded on startup because {rn} was less than {groups[_Index].engines[n].ignitfailure / shutdownexplosion}");
                                RemoveEngine(m, n, _Index, true);
                            }
                            else if (rn < groups[_Index].engines[n].ignitfailure)
                            {
                                this.Log(true, $"disabled on startup because {rn} was less than {groups[_Index].engines[n].ignitfailure}");
                                RemoveEngine(m, n, _Index, false);
                            }
                        }
                        else
                        {
                            if (rn < groups[_Index].engines[n].failure / (shutdownallexplosion * (groups[_Index].engines[n].autoShutDown ? autoshutdownbenefit : 1)))
                            {
                                this.Log(true, $"exploded all because {rn} was less than {groups[_Index].engines[n].failure / shutdownallexplosion}");
                                RemoveEngine(n, _Index, true);
                            }
                            else if (rn < groups[_Index].engines[n].failure / (shutdownexplosion * (groups[_Index].engines[n].autoShutDown ? autoshutdownbenefit : 1)))
                            {
                                this.Log(true, $"exploded because {rn} was less than {groups[_Index].engines[n].failure / shutdownexplosion}");
                                RemoveEngine(m, n, _Index, true);
                            }
                            else if (rn < groups[_Index].engines[n].failure)
                            {
                                this.Log(true, $"disabled because {rn} was less than {groups[_Index].engines[n].failure}");
                                RemoveEngine(m, n, _Index, false);
                            }
                        }
                    }
                    else if (!groups[_Index].engines[n].autoShutDown && groups[_Index].engines[n].disableIndex.Contains(m))
                    {
                        if (_startUp)
                        {
                            if (rn < 1 / shutdownallexplosion)
                            {
                                this.Log(true, $"exploded all on startup because {rn} was greater than {2 / shutdownallexplosion}");
                                RemoveEngine(n, _Index, true);
                            }
                            else if (rn < 1 / shutdownexplosion)
                            {
                                this.Log(true, $"exploded on startup because {rn} was greater than {2 / shutdownexplosion}");
                                RemoveEngine(m, n, _Index, true);
                            }
                        }
                        else
                        {
                            if (rn < 1 / shutdownallexplosion)
                            {
                                this.Log(true, $"exploded all because {rn} was greater than {2 / shutdownallexplosion}");
                                RemoveEngine(n, _Index, true);
                            }
                            else if (rn < 1 / shutdownexplosion)
                            {
                                this.Log(true, $"exploded because {rn} was greater than {2 / shutdownexplosion}");
                                RemoveEngine(m, n, _Index, true);
                            }
                        }
                    }
                }
        }

        public void FixedUpdate()
        {
#if Timed
            timer.Start();
#endif

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    double mult = ((vessel.angularVelocity.magnitude > 2) ? vessel.angularVelocity.magnitude : 2)/2;
                    if (groups[i].hasFailure && groups[i].oldThrottle == 0 && groups[i].engineModule.currentThrottle > groups[i].oldThrottle)
                        CheckForFailures(i, true, mult);
                    else if (groups[i].hasFailure && groups[i].oldThrottle != 0 && checkedFailure + 1 < Planetarium.GetUniversalTime())
                        CheckForFailures(i, false, mult);

                    //TODO make this work for multiple engine groups
                    if (groups[i].engineModule.currentThrottle != groups[i].oldThrottle)
                    {
                        part.Effects.Event("running", groups[i].engineModule.currentThrottle, 0);
                    }

                    groups[i].oldThrottle = groups[i].engineModule.currentThrottle;

                }

                if (fixedUpdates == 10)
                {
                    ConfigurableJoint[] joints = part.GetComponents<ConfigurableJoint>();
                    foreach (ConfigurableJoint joint in joints)
                    {
                        JointDrive drive = joint.xDrive;
                        drive.maximumForce = drive.maximumForce * 10;
                        drive.positionDamper = drive.positionDamper / 10;
                        drive.positionSpring = drive.positionSpring / 10;
                        drive = joint.yDrive;
                        drive.maximumForce = drive.maximumForce * 10;
                        drive.positionDamper = drive.positionDamper / 10;
                        drive.positionSpring = drive.positionSpring / 10;
                        drive = joint.zDrive;
                        drive.maximumForce = drive.maximumForce * 10;
                        drive.positionDamper = drive.positionDamper / 10;
                        drive.positionSpring = drive.positionSpring / 10;
                    }

                    foreach (EngineGroup group in groups)
                        foreach (ExtraEngine engine in group.engines)
                            engine.CheckEngines(part);

                    foreach (PartModule mod in part.Modules)
                        if (updateRemoveModules.Contains(mod.ClassName))
                            part.RemoveModule(mod);
                }
                fixedUpdates++;
            }

#if Timed
            this.Log(true, $"{timer.Stop()} for FixedUpdate on {part.name}");
#endif
        }
        public override void OnSave(ConfigNode node)
        {
            print(TAG + "OnSave");
            foreach (EngineGroup group in groups)
                node.AddNode(group.ExportGroupNode());
            base.OnSave(node);
        }
        public virtual void onEditorStartTweak()
        {
            print(TAG + "onEditorStartTweak");
        }
        public virtual void onEditorEndTweak()
        {
            print(TAG + "onEditorEndTweak");
        }
        public virtual void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            print(TAG + "onFlightStateSave");
        }
        public override void OnLoad(ConfigNode node)
        {
            print(TAG + "OnLoad");
            base.OnLoad(node);
            //if (HighLogic.LoadedScene == GameScenes.LOADING) return;
            savedConfig = node;
            //if (!loaded) LoadConfig(node);
        }
        public override void OnStart(StartState state)
        {
            print(TAG + "OnStart");
            base.OnStart(state);

            if (failureGenerator == null)
                failureGenerator = new System.Random();

            UrlConfig[] urlConfigs = GameDatabase.Instance.GetConfigs("PART");

            foreach (UrlConfig urlConfig in urlConfigs)
            {
                bool isEngine = false;
                bool isTank = false;
                if (urlConfig.config.GetNodes("RESOURCE").Length != 0)
                    isTank = true;
                foreach (ConfigNode node in urlConfig.config.GetNodes("MODULE"))
                {
                    if (node.GetValue("name") == "ModuleFuelTanks")
                        isTank = true;
                    else if (node.GetValue("name") == "ModuleEnginesFX" || node.GetValue("name") == "ModuleEnginesRF" || node.GetValue("name") == "ModuleEngineConfigs")
                        isEngine = true;
                }
                if (!isEngine || isTank) continue;

                //string[] url = urlConfig.url.Split('/');
                //int n = 0;
                //string urlstring = "";
                //while (n < url.Length - 3)
                //{
                //    urlstring += (url[n] + "/");
                //    n++;
                //}
                //urlstring += ("@thumbs/" + urlConfig.name.Replace('_', '.') + "_icon");
                //
                //Texture2D thumbnail = GameDatabase.Instance.GetTexture(urlstring, false);
                //if (thumbnail == null)
                //{
                //    urlstring += "0";
                //    thumbnail = GameDatabase.Instance.GetTexture(urlstring, false);
                //}

                urls.Add(urlConfig.url);
                names.Add(urlConfig.name);
                //thumbnails.Add(thumbnail);
            }
            ConfigNode config = this.GetConfig(part.partInfo.partConfig);
            if (!loaded)
            {
                if (savedConfig != null && savedConfig.HasNode("GROUP")) LoadConfig(savedConfig);
                else LoadConfig(config);
            }

            engineConfig = null;
            selectedEngine = -1;
            engineName = "";
            engineNames = new List<string>();

            if (groups[0].usePrimaryPlume && groups[0].clusterWaterfallConfig != null)
                Events["ToggleClusterPlume"].active = true;
            else
                Events["ToggleClusterPlume"].active = false;

            if (groups[0].hasFailure)
                Events["DisableAutoShutDown"].active = true;
            else
                Events["DisableAutoShutDown"].active = false;

            if (addModel != "")
                AddModel(addModel);
        }

        public void AddModel(string path)
        {
            //this.Log(true, GameDatabase.Instance.ExistsModel(path).ToString());
            //GameObject model = GameDatabase.Instance.GetModel(path);
            //model.transform.SetParent(part.transform.FindChild("model"));
            //model.SetActive(true);

            print(Application.dataPath);
            print(Path.Combine(Application.dataPath, "../GameData", path));
            AssetBundle myLoadedAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.dataPath, "../GameData", path));
            if (myLoadedAssetBundle == null)
            {
                Debug.Log("Failed to load AssetBundle!");
                return;
            }

            GameObject[] prefabs = myLoadedAssetBundle.LoadAllAssets<GameObject>();
            foreach (GameObject prefab in prefabs)
            {
                GameObject instantiated = Instantiate(prefab);
                instantiated.transform.SetParent(part.transform.FindChild("model"));
                instantiated.transform.localPosition = Vector3.zero;
            }

            myLoadedAssetBundle.Unload(false);
        }

        public void AddEngine(ConfigNode Config, string Name, Vector3 position, Vector3 angles, int symmetry, string axis, List<int> removeIndex = null, List<int> disableIndex = null)
        {
            string model = Config.GetNode("MODEL").GetValue("model");
            GameObject engine = GameDatabase.Instance.GetModel(model);
            Vector3 scale = ConfigNode.ParseVector3(Config.GetNode("MODEL").GetValue("scale"));

            if (selectedGroup == -1) AddGroup();
            groups[selectedGroup].AddEngine(HighLogic.LoadedSceneIsEditor, Name, float.Parse(Config.GetValue("mass")), position, angles, scale, symmetry, axis, engine, part, activeConfig, removeIndex, disableIndex);
            groups[selectedGroup].ChangeThrustTransforms($"thrustTransform{groups.Count-1}");

        }

        public ConfigNode HandleLookAtConstraint(ConfigNode _module, int _index, ConfigNode oldconfig)
        {
            ConfigNode output = oldconfig;
            if (output == null)
            {
                output = new ConfigNode("MODULE");
                output.AddValue("name", "FXModuleLookAtConstraint");
            }

            for (int i = 0; i < _module.GetNodes("CONSTRAINLOOKFX").Count<ConfigNode>(); i++)
            {
                string target = _module.GetNodes("CONSTRAINLOOKFX")[i].GetValue("targetName");
                string rotator = _module.GetNodes("CONSTRAINLOOKFX")[i].GetValue("rotatorsName");
                ConfigNode tmp = groups[_index].GetLookAtConstraints(target, rotator, new Vector3(), _index);
                foreach (ConfigNode tmpNode in tmp.nodes)
                    output.AddNode(tmpNode);
            }
            return output;
        }

        public void AddModules(ConfigNode node, StartState state, int _index)
        {
            this.Log(true, node.ToString());
            PartModule[] Modules = part.GetComponents<PartModule>();
            foreach (ConfigNode effectsNode in node.GetNodes("EFFECTS"))
            {
                //this.Log(true, effectNode.GetNode("running").ToString());
                //
                //ConfigNode runningNode = new ConfigNode("EFFECTS");
                //runningNode.AddNode(effectNode.GetNode("running"));
                //
                //this.Log(true, runningNode.ToString());
                foreach (ConfigNode effectNode in effectsNode.GetNodes())
                {
                    this.Log(true, effectNode.ToString());
                    string[] volumes = effectNode.GetNode("AUDIO").GetValues("volume");
                    for (int i = 0; i < volumes.Count(); i++)
                    {
                        this.Log(true, volumes[i]);
                        if (volumes[i].Contains(" "))
                        {
                            Vector2 volume = ConfigNode.ParseVector2(volumes[i]);
                            //TODO sum this up
                            volume.y *= groups[0].engines[0].symmetry;

                            volumes[i] = volume.ToString().Replace("(", "").Replace(")","");
                            effectNode.GetNode("AUDIO").SetValue("volume", volumes[i], i);
                        }
                        else
                        {
                            float volume = float.Parse(volumes[i]);
                            //TODO sum this up
                            volume *= groups[0].engines[0].symmetry;

                            volumes[i] = volume.ToString();
                            effectNode.GetNode("AUDIO").SetValue("volume", volumes[i], i);
                        }
                        this.Log(true, volumes[i]);
                    }
                    this.Log(true, effectNode.ToString());
                }

                effectConfig = effectsNode;
                part.LoadEffects(effectsNode);
                part.InitializeEffects();
            }
            foreach (ConfigNode moduleNode in node.GetNodes("MODULE"))
            {
                PartModule newmodule = null;
                this.Log(true, $"Loading module: {moduleNode.GetValue("name")}");

                if (moduleNode.GetValue("name") == "ModuleWaterfallFX" && groups[_index].secondaryWaterfallConfig == null)
                    groups[_index].secondaryWaterfallConfig = moduleNode.CreateCopy();

                if (moduleNode.GetValue("name") == "ModuleGimbal" && groups[_index].gimbalConfig == null)
                    groups[_index].gimbalConfig = moduleNode.CreateCopy();

                if (moduleNode.GetValue("name") == "ModuleTestLite")
                {
                    foreach (ExtraEngine eng in groups[_index].engines)
                        if (moduleNode.GetValue("configuration") == eng.cfg.engineConfig)
                        {
                            eng.failure = moduleNode.HasNode("reliabilityCurve") ? double.Parse(moduleNode.GetNode("reliabilityCurve").GetValue("key").Split(' ')[1]) : 0;
                            eng.ignitfailure = moduleNode.HasNode("ignitionCurve") ? 1 - double.Parse(moduleNode.GetNode("ignitionCurve").GetValue("key").Split(' ')[1]) : 0;
                            this.Log(true, $"Enigne failure chance at {eng.ignitfailure * 100}%");
                            this.Log(true, $"Igniton failure chance at {eng.ignitfailure * 100}%");

                            groups[_index].hasFailure = true;
                        }
                }

                if (moduleNode.GetValue("name") == "TestFlightReliability")
                {
                    foreach (ExtraEngine eng in groups[_index].engines)
                        foreach (ConfigNode config in moduleNode.GetNodes("CONFIG"))
                            if (config.GetValue("configuration") == eng.cfg.engineConfig)
                            {
                                eng.failure = config.HasNode("reliabilityCurve") ? double.Parse(config.GetNode("reliabilityCurve").GetValue("key").Split(' ')[1]) : 0;
                                this.Log(true, $"Enigne failure chance at {eng.ignitfailure * 100}%");

                                groups[_index].hasFailure = true;
                            }
                }
                if (moduleNode.GetValue("name") == "TestFlightFailure_IgnitionFail")
                {
                    foreach (ExtraEngine eng in groups[_index].engines)
                        foreach (ConfigNode config in moduleNode.GetNodes("CONFIG"))
                            if (config.GetValue("configuration") == eng.cfg.engineConfig)
                            {
                                eng.ignitfailure = config.HasNode("baseIgnitionChance") ? 1 - double.Parse(config.GetNode("baseIgnitionChance").GetValue("key").Split(' ')[1]) : 0;
                                this.Log(true, $"Igniton failure chance at {eng.ignitfailure * 100}%");

                                groups[_index].hasFailure = true;
                            }
                }

                if (addModules.Contains(moduleNode.GetValue("name")))
                {
                    this.Log(true, "Evaluating: " + moduleNode.GetValue("name"));
                    bool exists = false;

                    if (moduleNode.GetValue("name") == "FXModuleLookAtConstraint")
                    {
                        lookatconstraint = HandleLookAtConstraint(moduleNode, groups.Count - 1, lookatconstraint);
                    }
                    //if (moduleNode.GetValue("name") == "ModuleEnginesRF")
                    //{
                    //    newmodule = part.AddModule(moduleNode, false);
                    //}

                    foreach (PartModule module in Modules)
                    {
                        if (module.ClassName == moduleNode.GetValue("name"))
                        {
                            this.Log(true, "Loading: " + moduleNode.GetValue("name"));

                            if (moduleNode.GetValue("name") == "FXModuleLookAtConstraint")
                            {
                                module.OnLoad(lookatconstraint);
                                module.OnStart(state);
                            }
                            else
                            {
                                module.OnLoad(moduleNode);
                                module.OnStart(state);
                            }
                            if (moduleNode.GetValue("name") != "ModulePekkaAnim" || moduleNode.GetValue("Different1") == ((ModulePekkaAnim.ModulePekkaAnim)module).Different1)
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                    if (exists == false)
                    {
                        this.Log(true, "Adding: " + moduleNode.GetValue("name"));
                        newmodule = part.AddModule(moduleNode, false);
                    }
                }
                if (removeModules.Contains(moduleNode.GetValue("name")))
                {
                    foreach (PartModule module in Modules)
                    {
                        if (module.ClassName == moduleNode.GetValue("name"))
                        {
                            this.Log(true, "Removing: " + moduleNode.GetValue("name"));
                            part.RemoveModule(module);
                            break;
                        }
                    }
                }
            }

            AddEngineModules(state, _index, true);//node, 
        }

        public void AddEngineModules(StartState state, int _index, bool _first, bool _updatePrimary = true, bool _updateCluster = true)
        {
            mass = 0;
            foreach (EngineGroup group in groups)
                mass += group.mass;
            part.prefabMass = mass;
            part.UpdateMass();
            part.maxTemp = 1200;
            part.skinMaxTemp = 1200;

            loaded = true;
            //remove.DestroyGameObject();

            engineCfg = new EngineCFG();
            engineCfg.AddCFG(groups[_index].groupConfig);
            engineCfg.CFG = Utils.MergeNodes(engineCfg.CFG, templateConfig);
            engineCfg.CFG.SetValue("engineID", "AddEngine");
            this.Log(true, engineCfg.CFG.ToString());
            ModuleEngines engineModule = groups[_index].engineModule;
            if (engineModule != null && !_first && state == StartState.Flying)
            {
                //this.Log(true, "remove Engine");
                //part.RemoveModule(engineModule);
                foreach (ConfigNode prop in engineCfg.CFG.GetNodes("PROPELLANT"))
                    if (prop.HasValue("DrawGauge"))
                        prop.RemoveValue("DrawGauge");
            }
            if (engineModule == null)
            {
                engineModule = (ModuleEngines)part.AddModule(engineCfg.CFG.CreateCopy(), false);
                groups[_index].engineModule = engineModule;
            }
            
            engineModule.engineID = "AddEngine";
            engineModule.thrustVectorTransformName = engineCfg.CFG.GetValue("thrustVectorTransformName");
            while(engineModule.atmosphereCurve.Curve.keys.Count() > 0)
                engineModule.atmosphereCurve.Curve.RemoveKey(0);
            
            int count = engineCfg.CFG.GetNode("atmosphereCurve").GetValues("key").Count();
            for (int i = 0; i < count; i++)
            {
                Vector2 vec2 = ConfigNode.ParseVector2(engineCfg.CFG.GetNode("atmosphereCurve").GetValues("key")[i]);
                Vector2 next = ConfigNode.ParseVector2(engineCfg.CFG.GetNode("atmosphereCurve").GetValues("key")[(i+1) % count]);
            
                Keyframe key = new Keyframe(vec2.x,vec2.y,next.y-vec2.y, next.y-vec2.y);
            
                engineModule.atmosphereCurve.Curve.AddKey(key);
            }
            if (engineCfg.throttleResponseRate != 0) engineModule.throttleResponseRate = float.Parse(engineCfg.CFG.GetValue("throttleResponseRate"));
            if (engineCfg.throttleStartedMult != 0) engineModule.throttleStartedMult = float.Parse(engineCfg.CFG.GetValue("throttleStartedMult"));
            if (engineCfg.throttleStartupMult != 0) engineModule.throttleStartupMult = float.Parse(engineCfg.CFG.GetValue("throttleStartupMult"));
            
            engineModule.OnLoad(engineCfg.CFG.CreateCopy());
            engineModule.OnStart(state);

            part.LoadEffects(effectConfig);
            part.InitializeEffects();

            ModuleGimbal gimbalModule = groups[_index].gimbalModule;
            if (gimbalModule == null && groups[_index].gimbalConfig != null)
            {
                gimbalModule = (ModuleGimbal)part.AddModule(groups[_index].gimbalConfig, false);
                groups[_index].gimbalModule = gimbalModule;
                gimbalModule.OnStart(state);
            }
            if (gimbalModule != null && _first)
                gimbalModule.OnStart(state);

            if (state == StartState.Flying)
            {
                //for (int i = 0; i < part.Modules.Count; i++)
                //{
                //    print($"{i} {part.Modules[i].ClassName}");
                //    if (part.Modules[i].ClassName == "ModuleEnginesRF")
                //    {
                //        ModuleEngines eModule = (ModuleEngines)(part.Modules[i]);
                //        print(eModule.maxThrust);
                //    }
                //}
                //PartModule temp = part.Modules[groupsNum - _index];
                //part.Modules.Remove(engineModule);
                //for (int i = 0; i < part.Modules.Count; i++)
                //{
                //    print($"{i} {part.Modules[i].ClassName}");
                //    if (part.Modules[i].ClassName == "ModuleEnginesRF")
                //    {
                //        ModuleEngines eModule = (ModuleEngines)(part.Modules[i]);
                //        print(eModule.maxThrust);
                //    }
                //}
                //
                //part.Modules[groupsNum - _index] = engineModule;
                //for (int i = 0; i < part.Modules.Count; i++)
                //{
                //    print($"{i} {part.Modules[i].ClassName}");
                //    if (part.Modules[i].ClassName == "ModuleEnginesRF")
                //    {
                //        ModuleEngines eModule = (ModuleEngines)(part.Modules[i]);
                //        print(eModule.maxThrust);
                //    }
                //}
                //
                //part.Modules.Add(temp);
                //for (int i = 0; i < part.Modules.Count; i++)
                //{
                //    print($"{i} {part.Modules[i].ClassName}");
                //    if (part.Modules[i].ClassName == "ModuleEnginesRF")
                //    {
                //        ModuleEngines eModule = (ModuleEngines)(part.Modules[i]);
                //        print(eModule.maxThrust);
                //    }
                //}
                addWaterfall(groups[_index], state, _updatePrimary, _updateCluster);
            }
            engineModule.engineID = $"{groups[_index].groupName}";
            engineCfg.CFG.SetValue("engineID", $"{groups[_index].groupName}");
        }

        public void AddGroup(string name = "", ConfigNode _primaryWaterfallConfig = null, ConfigNode _clusterWaterfallConfig = null)
        {
            List<PartModule> WaterfallModules = new List<PartModule>();
            PartModule[] Modules = part.GetComponents<PartModule>();
            foreach (PartModule module in Modules)
                if (module.ClassName == "ModuleWaterfallFX")
                    WaterfallModules.Add(module);

            PartModule WaterfallModule = (part.GetComponents<PartModule>().Count() > groups.Count) ? part.GetComponents<PartModule>()[groups.Count] : null;
            ModuleEngines engineModule = (part.GetComponents<ModuleEngines>().Count() > groups.Count) ? part.GetComponents<ModuleEngines>()[groups.Count] : null;
            ModuleGimbal gimbalModule = (part.GetComponents<ModuleGimbal>().Count() > groups.Count) ? part.GetComponents<ModuleGimbal>()[groups.Count] : null;

            if (name == "") name = $"{groups.Count}-Group";
            groups.Add(new EngineGroup(name, usePrimaryPlume, enableClusterPlume, engineModule, gimbalModule, _primaryWaterfallConfig, _clusterWaterfallConfig));

            selectedGroup = groups.Count - 1;
        }

        public void LoadConfig(ConfigNode node)
        {
            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : StartState.Flying;

            foreach (ConfigNode groupNode in node.GetNodes("GROUP"))
            {
                string name = (groupNode.GetValue("name") == null) ? "" : groupNode.GetValue("name");
                primaryWaterfallConfig = groupNode.GetNode("primaryWaterfallConfig");
                clusterWaterfallConfig = groupNode.GetNode("clusterWaterfallConfig");

                if (primaryWaterfallConfig != null) this.Log(true, primaryWaterfallConfig.ToString());
                if (clusterWaterfallConfig != null) this.Log(true, clusterWaterfallConfig.ToString());
                AddGroup(name, primaryWaterfallConfig, clusterWaterfallConfig);
                foreach (ConfigNode engineNode in groupNode.GetNodes("ENGINES"))
                {
                    string Name = engineNode.GetValue("Name");
                    if (names.Contains(Name))
                    {
                        SelectEngine(names.IndexOf(Name));
                        ConfigNode Config = GameDatabase.Instance.GetConfigNode(urls[selectedEngine]);
                        Vector3 position = ConfigNode.ParseVector3(engineNode.GetValue("Position"));
                        Vector3 angles = ConfigNode.ParseVector3(engineNode.GetValue("Rotation"));
                        int.TryParse(engineNode.GetValue("Symmetry"), out int symmetry);
                        string axis = (engineNode.GetValue("Axis") == null) ? "" : engineNode.GetValue("Axis");
                        List<int> removeIndex = new List<int>();
                        if (engineNode.GetValue("RemoveIndex") != null && engineNode.GetValue("RemoveIndex") != "")
                        {
                            List<string> removeString = engineNode.GetValue("RemoveIndex").Split(',').ToList();
                            foreach (string str in removeString)
                                removeIndex.Add(int.Parse(str));
                        }
                        List<int> disableIndex = new List<int>();
                        if (engineNode.GetValue("DisableIndex") != null && engineNode.GetValue("DisableIndex") != "")
                        {
                            List<string> disablString = engineNode.GetValue("DisableIndex").Split(',').ToList();
                            foreach (string str in disablString)
                                disableIndex.Add(int.Parse(str));
                        }

                        AddEngine(Config, Name, position, angles, symmetry, axis, removeIndex, disableIndex);

                        UpdateEngineCfg(engineNode.GetValue("ConfigName"));

                        AddModules(Config, sta, groups.Count-1);
                    }
                }
            }
        }

        public void EditEngines(int _GroupIndex, int _Index)
        {
            if (names.Contains(groups[_GroupIndex].engines[_Index].name)) SelectEngine(names.IndexOf(groups[groups.Count - 1].engines[_Index].name));

            pX = groups[_GroupIndex].engines[_Index].position.x.ToString();
            pY = groups[_GroupIndex].engines[_Index].position.y.ToString();
            pZ = groups[_GroupIndex].engines[_Index].position.z.ToString();

            rX = groups[_GroupIndex].engines[_Index].rotation.x.ToString();
            rY = groups[_GroupIndex].engines[_Index].rotation.y.ToString();
            rZ = groups[_GroupIndex].engines[_Index].rotation.z.ToString();

            symmetry = groups[_GroupIndex].engines[_Index].symmetry.ToString();
            axis = groups[_GroupIndex].engines[_Index].axis;

            enginesSelected = true;
            selectedEngines = _Index;
            selectedGroup = _GroupIndex;
        }

        public void SelectEngine(int Index)
        {
            selectedEngine = Index;
            engineName = names[Index];

            engineNames = new List<string>();

            ConfigNode Config = GameDatabase.Instance.GetConfigNode(urls[Index]);
            foreach (ConfigNode node in Config.GetNodes("MODULE"))
            {
                if (node.GetValue("name") == "ModuleEngineConfigs" && node.GetNodes("CONFIG").Length > 0)
                {
                    string defaultConfig = node.GetValue("configuration");
                    activeConfig = new EngineCFG(node.GetNode("CONFIG"));
                    foreach (ConfigNode cfg in node.GetNodes("CONFIG"))
                    {
                        if (cfg.GetValue("name") == defaultConfig)
                            activeConfig = new EngineCFG(cfg);
                        engineNames.Add(cfg.GetValue("name"));
                    }
                }
                if (node.GetValue("name") == "ModuleEnginesFX" || node.GetValue("name") == "ModuleEnginesRF")
                {
                    activeEngine = new EngineCFG(node);
                    templateConfig = node.CreateCopy();
                }
            }
            if (activeConfig != null)
                activeConfig.CompleteCFG(activeEngine);
            else
                activeConfig = activeEngine;

            enginesSelected = false;
        }

        public void UpdateEngineCfg(string name)
        {
            currentCfg = name;

            ConfigNode Config = GameDatabase.Instance.GetConfigNode(urls[selectedEngine]);
            foreach (ConfigNode node in Config.GetNodes("MODULE"))
                if (node.GetValue("name") == "ModuleEngineConfigs")
                    foreach (ConfigNode cfg in node.GetNodes())
                        if (cfg.GetValue("name") == currentCfg)
                            activeConfig = new EngineCFG(cfg, 1);
            activeConfig.CompleteCFG(activeEngine);
        }

        public void AddModel()
        {
            if (enginesSelected == true)
                return;
            if (selectedEngine == -1)
                return;

            ConfigNode Config = GameDatabase.Instance.GetConfigNode(urls[selectedEngine]);
            Vector3 position = new Vector3(float.Parse(pX), float.Parse(pY), float.Parse(pZ));
            Vector3 angles = new Vector3(float.Parse(rX), float.Parse(rY), float.Parse(rZ));
            AddEngine(Config, names[selectedEngine], position, angles, int.Parse(symmetry), axis);

            engineCfg = new EngineCFG();
            foreach (EngineGroup group in groups)
                engineCfg.AddCFG(group.groupConfig);

            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : HighLogic.LoadedSceneIsFlight ? StartState.Flying : StartState.None;
            AddModules(Config, sta, selectedGroup);

            radius = 0;
            GUIUtility.systemCopyBuffer = ExportModule();
        }

        public void DeleteModel()
        {
            groups[selectedGroup].deleteEngine(selectedEngines);

            AddEngineModules(StartState.Editor, selectedGroup, true);
            GUIUtility.systemCopyBuffer = ExportModule();
        }

        public void EditGroup()
        {
            if (enginesSelected == false)
                return;
            if (selectedEngine == -1)
                return;
            ConfigNode Config = GameDatabase.Instance.GetConfigNode(urls[selectedEngine]);
            string model = Config.GetNode("MODEL").GetValue("model");
            GameObject engine = GameDatabase.Instance.GetModel(model);

            Vector3 position = new Vector3(float.Parse(pX), float.Parse(pY), float.Parse(pZ));
            Vector3 angles = new Vector3(float.Parse(rX), float.Parse(rY), float.Parse(rZ));
            Vector3 scale = ConfigNode.ParseVector3(Config.GetNode("MODEL").GetValue("scale"));

            groups[selectedGroup].Edit(selectedEngines, float.Parse(Config.GetValue("mass")), position, angles, scale, int.Parse(symmetry), axis, engine, part, engineConfig);

            engineCfg = new EngineCFG();
            foreach (EngineGroup group in groups)
                engineCfg.AddCFG(group.groupConfig);

            StartState sta = HighLogic.LoadedSceneIsEditor ? StartState.Editor : HighLogic.LoadedSceneIsFlight ? StartState.Flying : StartState.None;
            AddModules(Config, sta, selectedGroup);

            radius = 0;
            GUIUtility.systemCopyBuffer = ExportModule();
        }

        public string ExportModule()
        {
            ConfigNode output = new ConfigNode("MODULE");
            output.AddValue("name", "ModuleEngineWeld");
            foreach (EngineGroup group in groups)
                output.AddNode(group.ExportGroupNode());
            return output.ToString();
        }

        public void OnGUI()
        {
            if (showUI)
            {
                if (HighLogic.LoadedScene == GameScenes.EDITOR)
                    engineWindowPosition = GUI.Window(97, engineWindowPosition, OnEngineWindow, "Add Engine");
                if (HighLogic.LoadedScene == GameScenes.EDITOR)
                    editWindowPosition = GUI.Window(96, editWindowPosition, OnEditWindow, "Edit Engines");
            }
        }

        private void OnEngineWindow(int windowId)
        {
            //GUILayout.Label("This is a label");

            GUILayout.BeginHorizontal(GUILayout.Width(850));

            enginePosition = GUILayout.BeginScrollView(enginePosition, GUILayout.Width(560f));
            for (int i = 0; i * 4 < urls.Count; i++)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(520f));
                if (i * 4 + 0 < urls.Count)
                    if (GUILayout.Button(names[i * 4 + 0], GUILayout.Width(130f)))
                        SelectEngine(i * 4 + 0);
                if (i * 4 + 1 < urls.Count)
                    if (GUILayout.Button(names[i * 4 + 1], GUILayout.Width(130f)))
                        SelectEngine(i * 4 + 1);
                if (i * 4 + 2 < urls.Count)
                    if (GUILayout.Button(names[i * 4 + 2], GUILayout.Width(130f)))
                        SelectEngine(i * 4 + 2);
                if (i * 4 + 3 < urls.Count)
                    if (GUILayout.Button(names[i * 4 + 3], GUILayout.Width(130f)))
                        SelectEngine(i * 4 + 3);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginVertical();

            if (GUILayout.Button("x")) showUI = !showUI;
            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("Translation: ", GUILayout.Width(80f));
            GUILayout.Label("x: ");
            pX = GUILayout.TextField(pX, GUILayout.Width(50f));
            GUILayout.Label("y: ");
            pY = GUILayout.TextField(pY, GUILayout.Width(50f));
            GUILayout.Label("z: ");
            pZ = GUILayout.TextField(pZ, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("Rotation: ", GUILayout.Width(80f));
            GUILayout.Label("x: ");
            rX = GUILayout.TextField(rX, GUILayout.Width(50f));
            GUILayout.Label("y: ");
            rY = GUILayout.TextField(rY, GUILayout.Width(50f));
            GUILayout.Label("z: ");
            rZ = GUILayout.TextField(rZ, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Width(250f));
            GUILayout.Label("Symmetry: ", GUILayout.Width(80f));
            symmetry = GUILayout.TextField(symmetry);
            if (int.Parse(symmetry) < 1) symmetry = "1";
            GUILayout.Label("Mirror Axis: ");
            axis = GUILayout.TextField(axis);
            GUILayout.EndHorizontal();

            GUILayout.Space(20f);
            foreach (string name in engineNames)
            {
                if (GUILayout.Button(name)) UpdateEngineCfg(name);
            }
            GUILayout.Space(40f);
            if (!enginesSelected) if (GUILayout.Button("Add Engine " + engineName)) AddModel();
            if (enginesSelected) if (GUILayout.Button("Edit Engine " + engineName)) EditGroup();
            if (enginesSelected) if (GUILayout.Button("Remove Engine " + engineName)) DeleteModel();

            GUILayout.Space(40f);
            if (selectedGroup != -1) groups[selectedGroup].groupName = GUILayout.TextField(groups[selectedGroup].groupName, GUILayout.Width(220f));
            if (GUILayout.Button("Add Group")) AddGroup();

            GUILayout.FlexibleSpace();

            groupPosition = GUILayout.BeginScrollView(groupPosition, GUILayout.Width(230f));
            for (int i = 0; i < groups.Count; i++)
            {
                if (GUILayout.Button(groups[i].groupName)) selectedGroup = i;
                for (int n = 0; n < groups[i].engines.Count; n++)
                {
                    GUILayout.BeginHorizontal(GUILayout.Width(200f));
                    GUILayout.Space(40f);

                    if (GUILayout.Button(groups[i].engines[n].name)) EditEngines(i, n);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void OnEditWindow(int windowId)
        {
            int n = 0;
            for (int x = 0; x < groups.Count; x++)
                for (int y = 0; y < groups[x].engines.Count; y++)
                {
                    for (int i = 0; i < groups[x].engines[y].symmetry; i++)
                    {
                        if (isHidingEngine.Count == n) isHidingEngine.Add(!groups[x].engines[y].removeIndex.Contains(i));
                        Vector2 position = groups[x].engines[y].GetPosition(i);
                        if (position.magnitude > radius)
                            radius = position.magnitude;
                        isHidingEngine[n] = GUI.Toggle(new Rect((position * 70 / radius) + new Vector2(90, 100), new Vector2(20, 20)), isHidingEngine[n], "");
                        if (isHidingEngine[n])
                            groups[x].showEngine(i, y);
                        if (!isHidingEngine[n])
                            groups[x].hideEngine(i, y);
                        n++;
                    }
                }
            GUI.DragWindow();
        }
    }
}
