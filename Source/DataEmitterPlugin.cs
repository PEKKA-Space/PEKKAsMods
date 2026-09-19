using CommNet.Network;
using Smooth.Algebraics;
using System;
using System.IO;
using System.Net.Sockets;
using static KSP.UI.UITransitionBase;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Linq;
using static TMPro.TMP_DefaultControls;
using System.Collections.Concurrent;

namespace ksp2_data_emitter_plugin
{
    public class ksp2_data_emitter_plugin : PartModule
    {
        const string TAG = "---TESTPLUGIN---";
        const string groupName = "DATAEMITTER";

        TcpListener server = null; // the server, use static to avoid multiple servers
        string http = null; // the recent data as ready formatted http message 

        //Editable Fields
        [KSPField(guiActive = false, isPersistant = false)]
        public string Propellants = "ElectricCharge, MonoPropellant, Liquid Fuel, Oxidizer";

        [KSPField(guiActive = false, isPersistant = false)]
        public float ignitionThresh = 0f;

        [KSPField(guiActive = false, isPersistant = false)]
        public int serverPort = 2025;

        [KSPField(guiActive = false, isPersistant = false)]
        public int TM = 0;

        bool isUpdatingData = false;
        bool isInCountdown = false;
        double Launch = 0;
        double currentTM = 0;

        //Creating Lists
        List<AttachNode> Nodes;
        public List<string> prop = new List<string>();
        List<ModuleEngines> Engines = new List<ModuleEngines>();

        public Dictionary<string, object> values = new Dictionary<string, object>();

        [KSPEvent(guiActive = true, guiName = "Start Countdown", active = true, groupName = groupName, groupDisplayName = groupName)]
        protected void CountdownStart()
        {
            if (TM != 0)
            {
                Launch = Planetarium.GetUniversalTime() - currentTM;
                isInCountdown = true;
                UpdateEventsCountdown(isInCountdown);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Reset Countdown", active = false, groupName = groupName, groupDisplayName = groupName)]
        protected void CountdownStop()
        {
            if (TM != 0)
            {
                currentTM = -TM;
                isInCountdown = false;
                UpdateEventsCountdown(isInCountdown);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Hold", active = false, groupName = groupName, groupDisplayName = groupName)]
        protected void CountdownHold()
        {
            if (TM != 0)
            {
                currentTM = Planetarium.GetUniversalTime() - Launch;
                isInCountdown = false;
                UpdateEventsHold(isInCountdown);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Release Hold", active = false, groupName = groupName, groupDisplayName = groupName)]
        protected void CountdownUnHold()
        {
            if (TM != 0)
            {
                Launch = Planetarium.GetUniversalTime() - currentTM;
                isInCountdown = true;
                UpdateEventsHold(isInCountdown);
            }
        }

        private void UpdateEventsCountdown(bool Activated)
        {
            Events["CountdownStart"].active = !Activated;
            Events["CountdownStop"].active = Activated;
            Events["CountdownHold"].active = Activated;
        }

        private void UpdateEventsHold(bool Activated)
        {
            Events["CountdownHold"].active = Activated;
            Events["CountdownUnHold"].active = !Activated;
        }

        protected void stopServer()
        {
            if (server != null)
            {
                print(TAG + " Stop Server");
                try
                {
                    server.Stop();
                }
                catch (Exception) { }
                server = null;
            }
        }

        // start the server and wait for clients but async (automatically in own thread):
        protected async void startServerAsync()
        {
            stopServer();
            print(TAG + " Start new Server " + serverPort);
            server = TcpListener.Create(serverPort);
            server.Start();
            while (true)
            {
                print(TAG + " [Server] waiting for new client...");
                using (var tcpClient = await server.AcceptTcpClientAsync())
                {
                    try
                    {
                        print(TAG + " [Server] Client has connected");
                        using (var networkStream = tcpClient.GetStream())
                        using (var reader = new StreamReader(networkStream))
                        using (var writer = new StreamWriter(networkStream) { AutoFlush = true })
                        {
                            // read request first:
                            bool emptyLine = false;
                            bool firstLine = true;
                            while (!emptyLine)
                            {
                                var request = await reader.ReadLineAsync();
                                var line = request.ToString();
                                if (firstLine)
                                {
                                    print(TAG + " [Server] Request from client " + request);
                                    firstLine = false;
                                }
                                emptyLine = line.Length == 0;
                            }
                            // answer:
                            await writer.WriteLineAsync(http);
                            // close the connection:
                            tcpClient.Close();
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(TAG + " [Server] client connection lost");
                    }
                }
            }
        }

        public string jsonValue(string name, object value)
        {
            return "\"" + name + "\":" + (value.GetType() == typeof(string) ? ("\"" + value + "\"") : value.ToString());
        }

        // get all new data and store it locally for faster access:
        public void updateData()
        {
            string json = "{\n";
            bool jsonEmpty = true;

            // clear old values:
            values.Clear();

            // fill global data:
            values["time"] = Planetarium.GetUniversalTime();    //If plugin cant find time something has gone horribly wrong

            if (isInCountdown)
            {
                values["tminus"] = Planetarium.GetUniversalTime()-Launch;
            }
            else
            {
                values["tminus"] = currentTM;
            }

            try { values["altitude"] = vessel.altitude; } catch (Exception) { }

            try { values["srfSpeed"] = vessel.srfSpeed; } catch (Exception) { }

            Vector3 rotation = Vector3.zero;    //If in editor or rotation not accesible defaults to 0
            try { rotation = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * Quaternion.LookRotation(vessel.north, (vessel.CoMD - vessel.mainBody.position).normalized)).eulerAngles; } catch (Exception) { } // i just copied mechjeb, way too lazy to do all this myself // mechjeb implementation
            try { values["Pitch"] = (rotation.x > 180) ? 360.0 - rotation.x : -rotation.x; } catch (Exception) { }
            try { values["Heading"] = rotation.y; } catch (Exception) { }
            try { values["Roll"] = (rotation.z > 180) ? 360.0 - rotation.z : -rotation.z; } catch (Exception) { }

            foreach (PartResource resource in part.Resources)
            {
                if (prop.Contains(resource.resourceName))
                {
                    try { values[resource.resourceName + "_amount"] = resource.amount; } catch (Exception) { }
                    try { values[resource.resourceName + "_max"] = resource.maxAmount; } catch (Exception) { }
                }
            }
            int i = 0;
            string IgnitedEngines = "";
            //UInt64 IgnitedEngines = 0;
            //UInt64 value = 1;
            while (i < Engines.Count)
            {
                //print(Engines[i].finalThrust); //thrust for future use
                //print(i + "Throttle" + Engines[i].currentThrottle);
                if (Engines[i].currentThrottle > ignitionThresh)
                {
                    IgnitedEngines = IgnitedEngines + "1";
                    //IgnitedEngines += (value << i);
                    //print(IgnitedEngines);
                }
                else
                {
                    IgnitedEngines = IgnitedEngines + "0";
                }
                i = i + 1;
            }
            try { values["ActiveEngines"] = IgnitedEngines; } catch (Exception) { }

            // fill all data into the json string:
            foreach (KeyValuePair<string, object> v in values)
            {
                if (jsonEmpty) jsonEmpty = false; else json += ",\n";
                json += " " + jsonValue(v.Key, v.Value);
            }
            json += "\n}";
            //print(json); //debug json creation

            //Talking to browser to make it happy
            http =
            "HTTP / 1.1 200 OK\r\n"
            + "Content-Type: application/json\r\n"
                + "Access-Control-Allow-Origin: *\r\n"
                + "Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n"
                + "Access-Control-Allow-Headers: X-PINGOTHER, Content-Type\r\n"
                + "\r\n"
                + json
                + "\r\n"
                + "\r\n";
        }

        public void RecalculateEngines()
        {
            Engines = new List<ModuleEngines>();
            Nodes = part.attachNodes;
            foreach (AttachNode Node in Nodes)
            {
                if (Node.attachedPart != null)
                {
                    List<ModuleEngines> PartEngines = Node.attachedPart.FindModulesImplementing<ModuleEngines>();
                    if (PartEngines.Count() != 0)
                    {
                        foreach (ModuleEngines Engine in PartEngines)
                        {
                            Engines.Add(Engine);
                        }
                    }
                }
            }
        }

        public void FixedUpdate()
        {
            if (isUpdatingData)
            {
                updateData();
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            print(TAG + " OnStart " + state.ToString() + " state=" + ((int)state));

            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            RecalculateEngines();
            // fill the propellant info:
            char[] spearator = { ',', ';' };
            prop = Propellants.Split(spearator).ToList();
            int i = 0;
            currentTM = -TM;
            while (i < prop.Count)
            {
                prop[i] = prop[i].Trim();
                i = i + 1;
            }

            if ((state & StartState.Editor) != 0)
            {
                stopServer();
                isUpdatingData = true;
            }
            else
            if ((state & StartState.PreLaunch) != 0)
            {
                startServerAsync();
                isUpdatingData = true;
            }
        }

        public override void OnInactive()
        {
            base.OnInactive();
            print(TAG + " OnInactive ");
        }

        public void OnDestroy()
        {
            print(TAG + " OnDestroy ");
            stopServer();
        }

        public void OnVesselWasModified(Vessel Chnaged)
        {
            print(TAG + " Detected change in Vessel");
            if (Chnaged == vessel)
            {
                RecalculateEngines();
            }
        }
    }
}