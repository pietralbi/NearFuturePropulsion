﻿using KSPAPIExtensions;
/// VariablePowerEngine
/// ---------------------------------------------------
/// A module that allows the Power use and Isp of an engine to be varied via a GUI
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace NearFuturePropulsion
{
    public class VariablePowerEngine:PartModule
    {


        // Current power setting
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Power Level", guiFormat = "S2", guiUnits = "%")]
        [UI_FloatEdit(scene = UI_Scene.All, minValue = 0.0f, maxValue = 100, incrementLarge = 25.0f, incrementSmall = 5f, incrementSlide = 0.1f)]
        public float CurPowerSetting = 0f;

        [KSPField(isPersistant = false)]
        public FloatCurve HeatCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve PowerCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public FloatCurve IspCurve = new FloatCurve();

        [KSPField(isPersistant = false)]
        public float MaxPower;
        [KSPField(isPersistant = false)]
        public float MinPower;
        [KSPField(isPersistant = false)]
        public float MaxIsp;
        [KSPField(isPersistant = false)]
        public float MinIsp;

        // Link all engines
        [KSPField(isPersistant = true)]
        public bool LinkAllEngines = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Power Input", guiFormat = "S2", guiUnits = "Ec/s")]
        public float curPowerUse = 5f;

        private float lastPowerSetting = -1f;

        private Propellant ecPropellant;
        private Propellant fuelPropellant;

        private FloatCurve AtmoThrustCurve;
        private FloatCurve AtmoIspCurve;

        private List<VariablePowerEngine> allVariableEngines;
        private ModuleEnginesFX engine;

        public override string GetInfo()
        {
            return String.Format("Power Range: {0:F1} kN", PowerCurve.Evaluate(1f)) + "\n" +
                  String.Format("Isp Range: {0:F0} s", IspCurve.Evaluate(1f)) + "\n";
            
        }

        [KSPEvent(guiActive = true, guiName = "Link All Variable Engines", active = true)]
        public void LinkEngines()
        {
            foreach (VariablePowerEngine variableEngine in allVariableEngines)
            {
                variableEngine.LinkAllEngines = true;
            }
            LinkAllEngines = true;
        }

        [KSPEvent(guiActive = true, guiName = "Unlink All Variable Engines", active = false)]
        public void UnlinkEngines()
        {
            foreach (VariablePowerEngine variableEngine in allVariableEngines)
            {
                variableEngine.LinkAllEngines = false;
            }
            LinkAllEngines = false;
        }



        // Actions
        [KSPAction("Link Engines")]
        public void LinkEnginesAction(KSPActionParam param)
        {
            LinkEngines();
        }

        [KSPAction("Unlink Engines")]
        public void UnlinkEnginesAction(KSPActionParam param)
        {
            UnlinkEngines();
        }

        [KSPAction("Toggle Link Engines")]
        public void ToggleLinkEnginesAction(KSPActionParam param)
        {
            LinkAllEngines = !LinkAllEngines;
            foreach (VariablePowerEngine variableEngine in allVariableEngines)
            {
                variableEngine.LinkAllEngines = !variableEngine.LinkAllEngines;
            }
        }


        // Finds vVariablePowerEngines on the ship
        private void SetupVariableEngines()
        {
            allVariableEngines = new List<VariablePowerEngine>();
            List<Part> allParts = this.vessel.parts;
            foreach (Part pt in allParts)
            {
                PartModuleList pml = pt.Modules;
                for (int i = 0; i < pml.Count; i++)
                {
                    PartModule curModule = pml.GetModule(i);
                    VariablePowerEngine candidate = curModule.GetComponent<VariablePowerEngine>();
                    if (candidate != null && candidate != this && !allVariableEngines.Contains(candidate))
                        allVariableEngines.Add(candidate);
                }
            }
        }

        // Finds ModuleEnginesFX on the part
        private void LoadEngineModules()
        {
            engine = part.GetComponent<ModuleEnginesFX>();
            //PartModuleList modules = part.Modules;

            //foreach (PartModule mod in part.Modules)
            //{
            //    if (mod.moduleName == "ModuleEnginesFX")
            //    {
            //        engine=(ModuleEnginesFX)mod;
            //    }
            //}
        }


        public override void OnStart(PartModule.StartState state)
        {
            LoadEngineModules();
            SetupPropellants();
            if (engine == null)
            {
                Debug.Log("NFP: VaPIT: Engine Module not good");
                return;
            }

            if (state != StartState.Editor)
                SetupVariableEngines();

            ChangeIspAndPower(CurPowerSetting / 100f);
            CalculateCurves();
        }

        public override void OnUpdate()
        {
            if ((LinkAllEngines && Events["LinkEngines"].active) || (!LinkAllEngines && Events["UnlinkEngines"].active))
            {
                Events["LinkEngines"].active = !LinkAllEngines;
                Events["UnlinkEngines"].active = LinkAllEngines;
            }
        }
        int frameCounter = 0;

        public void FixedUpdate()
        {


            if (engine != null)
            {

                if (CurPowerSetting != lastPowerSetting)
                {
                    Debug.Log("Changed Power to " + CurPowerSetting.ToString());
                    AdjustVariablePower();
                }

                
                // Only run atmo tweaking in flight
                if (KSPAPIExtensions.PartUtils.IsLoaded(GameSceneFilter.Flight))
                {
                    if (frameCounter > 10)
                    {
                        //engine.maxThrust = AtmoThrustCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position));
                        engine.atmosphereCurve = new FloatCurve();
                        engine.atmosphereCurve.Add(0f, AtmoIspCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position)));
                        Debug.Log(AtmoIspCurve.Evaluate((float)FlightGlobals.getStaticPressure(vessel.transform.position)).ToString());
                        frameCounter = 0;
                    }
                    frameCounter++;
                }
            }

        }

        private void SetupPropellants()
        {


            //PowerCurve = new FloatCurve();
            //PowerCurve.Add(0f, MinPower);
            //PowerCurve.Add(1f, MaxPower);
            //IspCurve = new FloatCurve();
            //IspCurve.Add(0f, MinIsp);
            //IspCurve.Add(1f, MaxIsp);

            foreach (Propellant prop in engine.propellants)
            {
                if (prop.name != "ElectricCharge")
                {
                    fuelPropellant = prop;
                }
                else
                {
                    ecPropellant = prop;
                }
            }

            //Debug.Log("Changed mode to " + engine.engineID);
            //Debug.Log("Fuel: " + fuelPropellant.name);
            
            //Debug.Log("Isp Curve: " + IspCurve.Evaluate(0f) + " to " + IspCurve.Evaluate(1f));

            AdjustVariablePower();
        }
        private void AdjustVariablePower()
        {
            ChangeIspAndPower(CurPowerSetting / 100f);
            lastPowerSetting = CurPowerSetting;
            if (LinkAllEngines)
            {
                foreach (VariablePowerEngine variableEngine in allVariableEngines)
                {
                    variableEngine.ChangeIspAndPowerLinked(this, CurPowerSetting / 100f);
                }
            }
        }

        private void CalculateCurves()
        {
            AtmoThrustCurve = new FloatCurve();
            AtmoThrustCurve.Add(0f, engine.maxThrust);
            AtmoThrustCurve.Add(1f, 0f);

            AtmoIspCurve = new FloatCurve();
            AtmoIspCurve.Add(0f, engine.atmosphereCurve.Evaluate(0f));

            float rate = FindFlowRate(engine.maxThrust, engine.atmosphereCurve.Evaluate(0f), fuelPropellant);

            AtmoIspCurve.Add(1f, FindIsp(0f, rate, fuelPropellant));
        }

        public void ChangeIspAndPower(float level)
        {
            Debug.Log(engine.engineID);
            engine.atmosphereCurve = new FloatCurve();
            engine.atmosphereCurve.Add(0f, IspCurve.Evaluate(level));

            engine.heatProduction = HeatCurve.Evaluate(level);
            //engine.maxThrust = engine.maxThrust;

            curPowerUse = PowerCurve.Evaluate(level);

            //Debug.Log("Changed Isp to " + engine.atmosphereCurve.Evaluate(0f).ToString());
           // Debug.Log("Changed power use to " +curPowerUse.ToString());

            RecalculateRatios(curPowerUse, engine.atmosphereCurve.Evaluate(level));
        }

        public void ChangeIspAndPowerLinked(VariablePowerEngine other, float level)
        {
            if (this != other && CurPowerSetting != level * 100f)
                CurPowerSetting = level * 100f;
        }

        private void RecalculateRatios(float desiredPower, float desiredisp)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((engine.maxThrust * 1000f) / (desiredisp * 9.82d)) / (fuelDensity * 1000f);
            float ecRate = desiredPower / (float)fuelRate;

            fuelPropellant.ratio = 0.1f;
            ecPropellant.ratio = fuelPropellant.ratio * ecRate;

            CalculateCurves();
        }

        // finds the flow rate given thrust, isp and the propellant 
        private float FindFlowRate(float thrust, float isp, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double fuelRate = ((thrust * 1000f) / (isp * 9.82d)) / (fuelDensity * 1000f);
            return (float)fuelRate;
        }

        private float FindIsp(float thrust, float flowRate, Propellant fuelPropellant)
        {
            double fuelDensity = PartResourceLibrary.Instance.GetDefinition(fuelPropellant.name).density;
            double isp = (((thrust * 1000f) / (9.82d)) / flowRate) / (fuelDensity * 1000f);
            return (float)isp;
        }
    }
}
