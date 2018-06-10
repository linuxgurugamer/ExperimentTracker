using System;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using KSP.IO;

using UnityEngine;

using ClickThroughFix;
using ToolbarControl_NS;

namespace ExperimentTracker
{
    /** Load in Flight scene once */
    [KSPAddon(KSPAddon.Startup.Flight, false)]

    public class ExperimentTracker : MonoBehaviour
    {
        /** Used variables */
        //private static ApplicationLauncherButton etButton;
        private static ToolbarControl toolbarControl;

        private bool hideUI = false;
        private bool expGUI;
        private bool infGUI;
        private bool showFin;
        private float updateTime = 1f;
        private float timeSince = 0f;
        private string onActive;
        private string onInactive;
        private string onReady;
        private Vessel curVessel;
        private CelestialBody lastBody;
        private List<ModuleScienceExperiment> experiments;
        private List<ModuleScienceExperiment> possExperiments;
        private List<ModuleScienceExperiment> finishedExperiments;
        private List<IETExperiment> activators;
        private ExperimentSituations expSituation;
        private string curBiome;

        /** GUI stuff */
        private static float windowHeight = 0;
        private static float windowWidth = Screen.height / 5;
        private Rect expListRect = new Rect(0, 0, windowWidth, windowHeight);
        private Rect infRect = new Rect(0, 0, windowWidth, windowHeight);

        private string[] excludedExperiments = null;
       private string[] excludedManufacturers = null;

        public void ModuleManagerPostLoad()
        {
            if (excludedExperiments == null)
            {
                List<string> expList = new List<string>();
                ConfigNode[] excludedNode = GameDatabase.Instance.GetConfigNodes("KEI_EXCLUDED_EXPERIMENTS");

                if (excludedNode != null)
                {
                    for (int i = excludedNode.Length - 1; i >= 0; i--)
                    {
                        string[] types = excludedNode[i].GetValues("experiment");
                        expList.AddRange(types);
                    }
                }
                else
                    Log.Error("Missing config file");

                excludedExperiments = expList.Distinct().ToArray();

                foreach (var s in excludedExperiments)
                    Log.Info("Excluded experiment: " + s);

            }

            if (excludedManufacturers == null)
            {
                List<string> expList = new List<string>();
                ConfigNode[] excludedNode = GameDatabase.Instance.GetConfigNodes("KEI_EXCLUDED_MANUFACTURERS");
                if (excludedNode != null)
                {
                    for (int i = excludedNode.Length - 1; i >= 0; i--)
                    {
                        string[] types = excludedNode[i].GetValues("manufacturer");
                        expList.AddRange(types);
                    }
                }
                else
                    Log.Error("Missing config file");

                excludedManufacturers = expList.Distinct().ToArray();

                foreach (var s in excludedManufacturers)
                    Log.Info("Excluded manufacturer: " + s);
            }

        }

        private void OnGUI()
        {
            if (!hideUI)
            {
                if (expGUI)
                    expListRect = ClickThruBlocker.GUILayoutWindow(42, expListRect, mainWindow, Text.MODNAME);
                if (infGUI)
                    infRect = ClickThruBlocker.GUILayoutWindow(1337, infRect, infWindow, Text.INFO);
            }
        }

        /** The info UI */
        private void infWindow(int id)
        {
            if (infGUI)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                if (GUILayout.Button("Close"))
                    infGUI = false;
                GUILayout.Label("Biome: " + curBiome);
                GUILayout.Label("Situation: " + expSituation);
                GUILayout.Label("Body: " + lastBody);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUI.DragWindow();
            }
        }

        /** The main UI */
        private void mainWindow(int id)
        {
            if (expGUI)
            {
                bool hasPoss = possExperiments.Count > 0;
                bool hasFin = finishedExperiments.Count > 0;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Info"))
                    infGUI = !infGUI;
                if (GUILayout.Button("Close"))
                    expGUI = false;
                if (GUILayout.Button("Close all"))
                {
                    expGUI = false;
                    infGUI = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                if (hasPoss)
                    if (GUILayout.Button("Deploy all"))
                    {
                        List<string> deployed = new List<string>();
                        foreach (ModuleScienceExperiment e in possExperiments)
                        {
                            if (!deployed.Contains(e.experimentID))
                            {
                                Deploy(e);
                                deployed.Add(e.experimentID);
                            }
                        }
                    }
                GUILayout.Space(6);
                if (hasPoss)
                { List<string> listed = new List<string>();
                    foreach (ModuleScienceExperiment e in possExperiments)
                        if (!listed.Contains(e.experimentID))
                        {
                            if (GUILayout.Button(e.experimentActionName))
                                Deploy(e);
                            listed.Add(e.experimentID);
                        }
                }
                else
                {
                    GUILayout.Label(Text.NOTHING);
                }
                if (hasFin)
                {
                    GUILayout.Space(6);
                    if (GUILayout.Button(showFin ? "\u2191" + "Hide finished experiments" + "\u2191" : "\u2193" + "Show finished experiments" + "\u2193"))
                        showFin = !showFin;
                }
                if (showFin && hasFin)
                {
                    GUILayout.Space(6);
                    foreach (ModuleScienceExperiment e in finishedExperiments)
                    {
                        if (GUILayout.Button(e.experimentActionName))
                        {
                            if (Event.current.button == 0)
                            {
                                Review(e);
                            }
                            else if (Event.current.button == 1)
                            {
                                Reset(e);
                            }
                        }
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUI.DragWindow();
            }
        }

        /** Finds the current biome string */
        private string currentBiome()
        {
            if (curVessel != null)
                if (curVessel.mainBody.BiomeMap != null)
                    return !string.IsNullOrEmpty(curVessel.landedAt)
                        ? Vessel.GetLandedAtString(curVessel.landedAt)
                        : ScienceUtil.GetExperimentBiome(curVessel.mainBody,
                            curVessel.latitude, curVessel.longitude);
            return string.Empty;
        }

        /** Determines whether the status of the vessel has changed */
        private bool statusHasChanged()
        {
            return FlightGlobals.ActiveVessel.loaded && (curVessel != FlightGlobals.ActiveVessel || curBiome != currentBiome() ||
                expSituation != ScienceUtil.GetExperimentSituation(curVessel) || lastBody != curVessel.mainBody);
        }

        private bool timeIsUp()
        {
            if ((timeSince += Time.deltaTime) >= updateTime)
            {
                timeSince = 0f;
                return true;
            }
            return false;
        }

        /** Checks type for an experiment and returns suitable IETExperiment */
        private IETExperiment checkType(ModuleScienceExperiment exp)
        {
            foreach (IETExperiment act in activators)
            {
                try
                {
                    if (exp.GetType() == act.getType() || exp.GetType().IsSubclassOf(act.getType()))
                        return act;
                }
                catch (Exception)
                {
                    continue;
                }
            }
            return null;
        }

        /** Deploys an experiment */
        private void Deploy(ModuleScienceExperiment exp)
        {
            IETExperiment activator = checkType(exp);
            if (activator != null)
                activator.deployExperiment(exp);
        }

        /** Resets an experiment */
        private void Reset(ModuleScienceExperiment exp)
        {
            IETExperiment activator = checkType(exp);
            if (activator != null)
                activator.resetExperiment(exp);
        }

        /** Opens the reviewData dialog for an experiment */
        private void Review(ModuleScienceExperiment exp)
        {
            IETExperiment activator = checkType(exp);
            if (activator != null)
                activator.reviewData(exp);
        }

        /** Updates experiments and other fields */
        private void StatusUpdate()
        {
            timeSince = 0f;
            curVessel = FlightGlobals.ActiveVessel;
            curBiome = currentBiome();
            expSituation = ScienceUtil.GetExperimentSituation(curVessel);
            lastBody = curVessel.mainBody;
            experiments = getExperiments();
            possExperiments = new List<ModuleScienceExperiment>();
            finishedExperiments = new List<ModuleScienceExperiment>();
            IETExperiment activator;
            if (experiments.Count() > 0)
            {
                foreach (ModuleScienceExperiment exp in experiments)
                {
                    activator = checkType(exp);
                    if (activator != null)
                    {
                        if (activator.hasData(exp))
                        {
                            finishedExperiments.Add(exp);
                        }
                        else if (activator.checkExperiment(exp, expSituation, lastBody, curBiome))
                        {
                            possExperiments.Add(exp);
                        }
                    }
                }
            }
        }

        /** Called every frame */
        public void FixedUpdate()
        {
            if (statusHasChanged() || timeIsUp())
                StatusUpdate();
            if (possExperiments.Count > 0 && toolbarControl != null && !expGUI)
                toolbarControl.SetTexture(onReady + "-38", onReady + "-24");
            else if (toolbarControl != null)
                toolbarControl.SetTexture(getButtonTexture + "-38", getButtonTexture + "-24");
            expListRect.width = windowWidth;
            expListRect.height = windowHeight;

            /** Comment out the following code pre release!!! */
            /**
            if (Input.GetKeyDown(KeyCode.End))
            {
                ResearchAndDevelopment.Instance.CheatAddScience(100000);
                Reputation.Instance.AddReputation(100000, TransactionReasons.Cheating);
                Funding.Instance.AddFunds(100000, TransactionReasons.Cheating);
            }
            */
        }

        /** Gets all science experiments */
        private List<ModuleScienceExperiment> getExperiments()
        {
            List<ModuleScienceExperiment> experiments = new List<ModuleScienceExperiment>();

            var l = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
            foreach (var i in l)
            {
                if (i.experimentID != "" && !excludedExperiments.Contains(i.experimentID) &&
                    !excludedManufacturers.Contains(i.part.partInfo.manufacturer))
                    experiments.Add(i);
            }
            return experiments;
            //return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
        }

        /** Resets window positions if necessary */
        private void resetWindowPos()
        {
            if ((expListRect.x <= 0 || expListRect.y <= 0) || (expListRect.x >= Screen.width || expListRect.y >= Screen.height))
            {
                expListRect.x = Screen.width * 0.6f;
                expListRect.y = 0;
            }
            if ((infRect.x <= 0 || infRect.y <= 0) || (infRect.x >= Screen.width || infRect.y >= Screen.height))
            {
                infRect.x = Screen.width * 0.6f;
                infRect.y = 0;
            }
        }

        /** Called once at startup */
        public void Awake()
        {
            debugPrint("Awake()");

            /** Config loading setup */
            PluginConfiguration config = PluginConfiguration.CreateForType<ExperimentTracker>();
            config.load();
            expGUI = config.GetValue<bool>("expGUI");
            expListRect.x = config.GetValue<int>("expListRectX");
            expListRect.y = config.GetValue<int>("expListRectY");
            infGUI = config.GetValue<bool>("infGUI");
            infRect.x = config.GetValue<int>("infRectX");
            infRect.y = config.GetValue<int>("infRectY");
            resetWindowPos();

            /** Register for events */
            //GameEvents.onGUIApplicationLauncherReady.Add(setupButton);
            setupButton();
            GameEvents.onHideUI.Add(onHideUI);
            GameEvents.onShowUI.Add(onShowUI);
        }

        /** Called after Awake */
        public void Start()
        {
            debugPrint("Start()");

            /** Load textures */
            onActive = "ExperimentTracker/icons/ET_active";
            onInactive = "ExperimentTracker/icons/ET_inactive";
            onReady = "ExperimentTracker/icons/ET_ready";

            /** Initialize activators and add to activators list */
            activators = new List<IETExperiment>();
            activators.Add(new OrbitalScience());
            activators.Add(new StockScience()); // This MUST be the last one

            ModuleManagerPostLoad();
        }

        /** Called on destroy */
        public void OnDestroy()
        {
            debugPrint("OnDestroy()");

            /** Save to config */
            PluginConfiguration config = PluginConfiguration.CreateForType<ExperimentTracker>();
            config.SetValue("expGUI", expGUI);
            config.SetValue("expListRectX", (int)expListRect.x);
            config.SetValue("expListRectY", (int)expListRect.y);
            config.SetValue("infGUI", infGUI);
            config.SetValue("infRectX", (int)infRect.x);
            config.SetValue("infRectY", (int)infRect.y);
            config.save();

            /** Unregister for events */
            //GameEvents.onGUIApplicationLauncherReady.Remove(setupButton);

            /** Remove button */
#if false
            if (etButton != null)
                ApplicationLauncher.Instance.RemoveModApplication(etButton);
#endif
            toolbarControl.OnDestroy();
            Destroy(toolbarControl);
        }

        /** Set up for the toolbar-button */
        private void setupButton()
        {
#if false
            if (ApplicationLauncher.Ready)
            {

                ApplicationLauncher instance = ApplicationLauncher.Instance;
                if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
                {
                    if (etButton == null)
                    {
                        debugPrint("Setting up button");
                        etButton = instance.AddModApplication(toggleActive, toggleActive, null, null, null, null, 
            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
            getButtonTexture());
                    }
                    else
                    {
                        debugPrint("Button already set up");
                        etButton.onTrue = toggleActive;
                        etButton.onFalse = toggleActive;
                    }
                }
                else
                {
                    debugPrint("Removing button");
                    if (etButton != null)
                        instance.RemoveModApplication(etButton);
                    Destroy(this);
                }
            }
#endif
            if (toolbarControl == null)
            {
                toolbarControl = gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(toggleActive, toggleActive,
                    ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                    MODID,
                    "experimentTrackerButton",
                    getButtonTexture + "-38",
                    getButtonTexture + "-24",
                    MODNAME
                );

            }
        }
        internal const string MODID = "ExperimentTracker_NS";
        internal const string MODNAME = "Experiment Tracker";

        /** Get correct button texture */
        private string getButtonTexture
        {
            get
            {
                return expGUI ? onActive : onInactive;
            }
        }

        /** Called when button is pressed */
        private void toggleActive()
        {
            debugPrint("toggleAction()");
            expGUI = infGUI ? false : !expGUI;
            infGUI = false;
            toolbarControl.SetTexture(getButtonTexture + "-38", getButtonTexture + "-24");
        }

        /** Called when F2 is pressed and UI has been hided before.
         * onHideUI has been called before.
         */
        public void onShowUI()
        {
            hideUI = false;
        }

        /** Called when F2 is pressed and UI has been shown before */
        public void onHideUI()
        {
            hideUI = true;
        }

        /** Load and return a texture */
        private static Texture2D loadTexture(string path)
        {
            debugPrint("Loading (Texture): " + path);
            return GameDatabase.Instance.GetTexture(path, false);
        }

        /** Prints message to console */
        private static void debugPrint(string s)
        {
            print(Text.MODTAG + s);
        }
    }
}