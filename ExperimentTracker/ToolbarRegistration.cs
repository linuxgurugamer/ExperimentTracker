using UnityEngine;
using ToolbarControl_NS;

namespace ExperimentTracker
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(ExperimentTracker.MODID, ExperimentTracker.MODNAME);
        }
    }
}