using System;
using UnityEngine;

namespace ExperimentTracker
{
    class StockScience : IETExperiment
    {
        public bool checkExperiment(ModuleScienceExperiment exp, ExperimentSituations expSituation, CelestialBody lastBody, string curBiome)
        {            
            bool a = !exp.Inoperable && !exp.Deployed && exp.experiment.IsAvailableWhile(expSituation, lastBody)
                                && ResearchAndDevelopment.GetScienceValue(
                                exp.experiment.baseValue * exp.experiment.dataScale,
                                getExperimentSubject(exp.experiment, expSituation, lastBody, curBiome)) > 1f;
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && exp.experiment.id == "surfaceSample")
                a = a && checkSurfaceSample(lastBody);
            return a;
        }

        private bool checkSurfaceSample(CelestialBody lastBody)
        {
            if (GameVariables.Instance.GetScienceCostLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment)) >= 500)
                if (lastBody.bodyName == "Kerbin")
                {
                    return true;
                }
                else
                {
                    if (GameVariables.Instance.UnlockedEVA(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)))
                        return true;
                }
            return false;
        }

        private ScienceSubject getExperimentSubject(ScienceExperiment exp, ExperimentSituations expSituation, CelestialBody lastBody, string curBiome)
        {
            return ResearchAndDevelopment.GetExperimentSubject(exp, expSituation, lastBody, curBiome, curBiome);
        }

        public void deployExperiment(ModuleScienceExperiment exp)
        {
            exp.DeployExperiment();
        }

        public bool hasData(ModuleScienceExperiment exp)
        {
            return exp.GetData().Length > 0;
        }

        public void resetExperiment(ModuleScienceExperiment exp)
        {
            exp.ResetExperiment();
        }

        public void reviewData(ModuleScienceExperiment exp)
        {
            exp.ReviewData();
        }

        public Type getType()
        {
            return typeof(ModuleScienceExperiment);
        }
    }
}
