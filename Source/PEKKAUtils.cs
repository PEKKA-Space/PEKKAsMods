using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PEKKAUtils
{
    public static class Utils
    {
        public static void Log(this object context, bool _debug, string Message)
        {
            if (_debug)
                Debug.Log($"[{context.GetType().Name}] {Message}");
        }

        public static ConfigNode GetConfig(this object context, ConfigNode _partConfig)
        {
            ConfigNode config = _partConfig.CreateCopy();
            ConfigNode output = new ConfigNode();
            foreach (ConfigNode node in _partConfig.GetNodes("MODULE"))
                if (node.GetValue("name") == context.GetType().Name)
                {
                    output = node;
                    break;
                }

            return output;
        }

        public static List<ConfigNode> GetConfigs(this object context, ConfigNode _partConfig)
        {
            ConfigNode config = _partConfig.CreateCopy();
            List<ConfigNode> output = new List<ConfigNode>();
            foreach (ConfigNode node in config.GetNodes("MODULE"))
                if (node.GetValue("name") == context.GetType().Name)
                {
                    output.Add(node.CreateCopy());
                }

            return output;
        }

        public static (float, float) StringToXY(string String)
        {
            float x = 0;
            float y = 0;
            string[] floats = String.Split(' ');

            x = float.Parse(floats[0]);
            y = float.Parse(floats[1]);

            return (x, y);
        }

        public static Transform FindChild(Transform _transform, string _name)
        {
            if (_transform.name == _name)
                return _transform;

            for (int i = 0; i < _transform.childCount; i++)
            {
                Transform t = FindChild(_transform.GetChild(i), _name);
                if (t != null) return t;
            }

            return null;
        }

        public static List<Transform> FindChildren(Transform _transform, string _name, List<Transform> children)
        {
            if (_transform.name == _name)
                children.Add(_transform);

            for (int i = 0; i < _transform.childCount; i++)
            {
                children = FindChildren(_transform.GetChild(i), _name, children);
            }

            return children;
        }

        public static ConfigNode MergeNodes(ConfigNode _dominant, ConfigNode _cfg)
        {
            ConfigNode output = _dominant.CreateCopy();
            ConfigNode cfg = _cfg.CreateCopy();
            foreach (ConfigNode.Value v in cfg.values)
                if (!output.HasValue(v.name))
                    output.AddValue(v.name, v.value);
            return output;
        }

    }

    public class PerfTimer
    {
        System.Diagnostics.Stopwatch watch;
        public PerfTimer() { }

        public void Start()
        {
            watch = System.Diagnostics.Stopwatch.StartNew();
        }
        public string Stop()
        {
            watch.Stop();
            return ($" took {watch.ElapsedTicks} Ticks at a frequency of {System.Diagnostics.Stopwatch.Frequency}, {watch.ElapsedMilliseconds} Milliseconds");
        }
    }

    public class RenderLine
    {
        GameObject renderGameObject;
        LineRenderer lineRenderer;

        GameObject arrowRenderGameObject;
        LineRenderer arrowRenderer;

        GameObject arrowTwoRenderGameObject;
        LineRenderer arrowTwoRenderer;

        Camera cam;

        public RenderLine(Part _parent, string _name, Color _color)
        {
            if (HighLogic.LoadedSceneIsEditor)
                cam = EditorLogic.fetch.editorCamera;
            else if (HighLogic.LoadedSceneIsFlight)
                cam = FlightCamera.fetch.mainCamera;
            else
                cam = Camera.main;

            renderGameObject = new GameObject(_name);
            renderGameObject.transform.SetParent(_parent.transform);
            renderGameObject.SetActive(true);

            lineRenderer = renderGameObject.AddComponent<LineRenderer>();

            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = 0.25f;
            lineRenderer.endWidth = 0.25f;
            lineRenderer.widthMultiplier = 0.25f;

            lineRenderer.startColor = _color;
            lineRenderer.endColor = _color;

            arrowRenderGameObject = new GameObject(_name + "Arrow");
            arrowRenderGameObject.transform.SetParent(_parent.transform);
            arrowRenderGameObject.SetActive(true);

            arrowRenderer = arrowRenderGameObject.AddComponent<LineRenderer>();

            arrowRenderer.material = new Material(Shader.Find("Sprites/Default"));
            
            arrowRenderer.positionCount = 2;
            arrowRenderer.startWidth = 0.25f;
            arrowRenderer.endWidth = 0.25f;
            arrowRenderer.widthMultiplier = 0.25f;

            arrowRenderer.startColor = _color;
            arrowRenderer.endColor = _color;

            arrowTwoRenderGameObject = new GameObject(_name + "Arrow");
            arrowTwoRenderGameObject.transform.SetParent(_parent.transform);
            arrowTwoRenderGameObject.SetActive(true);

            arrowTwoRenderer = arrowTwoRenderGameObject.AddComponent<LineRenderer>();

            arrowTwoRenderer.material = new Material(Shader.Find("Sprites/Default"));

            arrowTwoRenderer.positionCount = 2;
            arrowTwoRenderer.startWidth = 0.25f;
            arrowTwoRenderer.endWidth = 0.25f;
            arrowTwoRenderer.widthMultiplier = 0.25f;

            arrowTwoRenderer.startColor = _color;
            arrowTwoRenderer.endColor = _color;
        } 

        public void Update(Vector3 _start, Vector3 _vec)
        {
            lineRenderer.SetPositions(new Vector3[] { _start, _start + _vec });
            arrowRenderer.SetPositions(new Vector3[] { _start + _vec, _start + _vec + (-_vec.normalized + Vector3.Cross(_vec, cam.transform.forward).normalized)/2 });
            arrowTwoRenderer.SetPositions(new Vector3[] { _start + _vec, _start + _vec + (-_vec.normalized - Vector3.Cross(_vec, cam.transform.forward).normalized)/2 });
        }

        public void setActive(bool _active)
        {
            renderGameObject.SetActive(_active);
            arrowRenderGameObject.SetActive(_active);
            arrowTwoRenderGameObject.SetActive(_active);
        }
    }

    public class Point
    {
        public float x = 0;
        public float y = 0;

        public Point(float _x, float _y)
        {
            x = _x;
            y = _y;
        }
        public Point()
        { }
    }

    public class Curve
    {
        public List<Point> points = new List<Point>();

        public float GetValue(float input)
        {
            //Optimization (i hope) if input is at a point
            if (points.Any(item => item.x == input))
            {
                Point point = points.Find(item => item.x == input);

                return point.y;
            }

            if (points.Count == 1)
                return points[0].y;

            float slope = 0;

            if (points.Count == 2 || input < points[1].x)
            {
                slope = (points[1].y - points[0].y) / (points[1].x - points[0].x);
                return points[0].y + slope * (input - points[0].x);
            }

            if (input > points[points.Count - 2].x)
            {
                slope = (points[points.Count - 1].y - points[points.Count - 2].y) / (points[points.Count - 1].x - points[points.Count - 2].x);
                return points[points.Count - 1].y + slope * (input - points[points.Count - 1].x);
            }

            float guess = 0.5f;
            float stepsize = 0.25f;
            int index = 0;

            while (true)
            {
                index = (int)Math.Floor((points.Count - 1) * guess);

                if (points[index].x > input)
                    guess = guess - stepsize;
                else if (points[index + 1].x < input)
                    guess = guess + stepsize;
                else
                {
                    slope = (points[index + 1].y - points[index].y) / (points[index + 1].x - points[index].x);
                    return points[index].y + slope * (input - points[index].x);
                }
                stepsize = stepsize / 2;
            }
        }

        public void AddPoint(string values)
        {
            float x = 0;
            float y = 0;
            (x, y) = Utils.StringToXY(values);
            points.Add(new Point(x, y));
        }

        public int OrderCurve(Point x, Point y)
        {
            // I really don't like these two functions to simply sort the configs
            if (x.x > y.x)
                return 1;
            else if (x.x < y.x)
                return -1;
            else
                return 0;
        }

        public void SortCurve()
        {
            points.Sort(OrderCurve);
        }
    }

    public class AnimationHelper
    {
        public string TAG = "[ANIMATIONHELPER]";
        public string animationName = "";
        public Animation[] animation = null;
        public float[] currentPos = null;
        public float[] currentSpeed = null;
        public bool startUp = true;

        public AnimationHelper(string _animationName, Part _part)
        {
            animationName = _animationName;
            animation = null;
            animation = _part.FindModelAnimators(animationName);
            currentSpeed = new float[animation.Length];
            currentPos = new float[animation.Length];
            foreach (Animation anim in animation)
            {
                anim.Stop(animationName);
            }
        }

        public bool SpeedChanged(int _Index, float _speed, float _negativespeed, bool _debug)
        {
            this.Log((_speed != currentSpeed[_Index] && currentSpeed[_Index] > 0) || (_negativespeed != -currentSpeed[_Index] && currentSpeed[_Index] < 0) && _debug, 
                $"SpeedChanged posSpeeddiff: {_speed - currentSpeed[_Index]} negSpeeddiff: {_negativespeed + currentSpeed[_Index]}");
            return ((_speed != currentSpeed[_Index] && currentSpeed[_Index] > 0) || (_negativespeed != -currentSpeed[_Index] && currentSpeed[_Index] < 0));
        }
        public bool PositionChanged(int _Index, float _position, float _currentPosition, bool _debug)
        {

            this.Log(((_currentPosition >= _position) && currentSpeed[_Index] > 0) || ((_currentPosition <= _position) && currentSpeed[_Index] < 0) && _debug, 
                $"PositionChanged posdiff: {_position}");

            return ((_currentPosition >= _position) && currentSpeed[_Index] > 0) || ((_currentPosition <= _position) && currentSpeed[_Index] < 0);
        }

        public bool MaxCatch(int _Index)
        {
            return currentSpeed[_Index] > 0 && animation[_Index][animationName].time == 0 && animation[_Index][animationName].enabled == false;
        }

        public bool EnableAnimation(int _Index, float _position, float _speed, float _currentPosition, bool _debug)
        {
            this.Log(_debug && animation[_Index][animationName].enabled == false && Math.Abs(_currentPosition - _position) > 0.01 * _speed, 
                $"{_Index} posdiff: {_currentPosition - _position} speed {0.01 * _speed}");

            return (animation[_Index][animationName].enabled == false
                && Math.Abs(_currentPosition - _position) > 0.01 * _speed);
        }

        public void Start(float _position, float _speed, bool _debug)
        {
            this.Log(_debug, $"startup Position {_position}");
            for (int i = 0; i < animation.Length; i++)
            {
                this.Log(true, $"{((_position) - (_speed) / 5)}");
                if (((_position * animation[i][animationName].length) - (_speed * animation[i][animationName].length) / 5) > 0)
                {
                    currentSpeed[i] = _speed;
                    animation[i][animationName].speed = (_speed * animation[i][animationName].length);
                    animation[i][animationName].time = (_position * animation[i][animationName].length) - (_speed * animation[i][animationName].length) / 5;

                    this.Log(_debug, $"startupspeed: {_speed}, time {(_position) - (_speed) / 5}");
                }
                else
                {
                    currentSpeed[i] = -_speed;
                    animation[i][animationName].speed = -(_speed * animation[i][animationName].length);
                    animation[i][animationName].time = (_position * animation[i][animationName].length) + (_speed * animation[i][animationName].length) / 5;

                    this.Log(_debug, $"startupspeed: {-_speed}, time {(_position) + (_speed) / 5}");
                }


                animation[i][animationName].enabled = true;
                animation[i].Play(animationName);
            }
            startUp = false;
        }

        public bool Update(float _position, float _speed, float _negativespeed, bool _debug = false)
        {
            bool stopped = false;
            for (int i = 0; i < animation.Length; i++)
            {
                if (startUp)
                    Start(_position, _speed, _debug);

                if (animation[i][animationName].enabled)
                    currentPos[i] = animation[i][animationName].time / animation[i][animationName].length;

                this.Log(_debug, $"{i} pos: {currentPos[i]}, _position {_position}");

                if (SpeedChanged(i, _speed, _negativespeed, _debug) || PositionChanged(i, _position, currentPos[i], _debug))
                {
                    this.Log(_debug, $"stopped {i} at: {currentPos[i]}");

                    currentSpeed[i] = 0;
                    currentPos[i] = animation[i][animationName].time / animation[i][animationName].length;

                    this.Log(_debug, $"stopped {i} at: {animation[i][animationName].time / animation[i][animationName].length}");

                    animation[i].Stop(animationName);

                    stopped = true;
                }
                if (MaxCatch(i))
                {
                    this.Log(_debug, $"maxed out {i}");

                    currentPos[i] = 1;
                    currentSpeed[i] = 0;
                    animation[i][animationName].time = animation[i][animationName].length;

                    stopped = true;
                }
                if (EnableAnimation(i, _position, _speed, currentPos[i], _debug))
                {
                    this.Log(_debug, $"started {i} position {_position}, speed {_speed}");

                    if (_position > currentPos[i])
                    {
                        animation[i][animationName].speed = animation[i][animationName].length * _speed;
                        currentSpeed[i] = _speed;
                    }
                    else if (_position < currentPos[i])
                    {
                        animation[i][animationName].speed = -animation[i][animationName].length * _negativespeed;
                        currentSpeed[i] = -_negativespeed;
                    }
                    animation[i][animationName].time = currentPos[i] * animation[i][animationName].length;
                    animation[i][animationName].enabled = true;
                    animation[i].Play(animationName);
                }
            }
            return stopped;
        }
    }
}
