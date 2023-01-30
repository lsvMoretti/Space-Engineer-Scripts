using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /// <summary>
        /// Name of the Horizontal Piston Group
        /// </summary>
        private static readonly string HorizontalGroupName = "Ice Drill Horizontal Pistons";

        /// <summary>
        /// Name of the Vertical Piston Group
        /// </summary>
        private static readonly string VerticalGroupName = "Ice Drill Vertical Pistons";

        /// <summary>
        /// Name of the Drill Group
        /// </summary>
        private static readonly string DrillGroupName = "Ice Drill Group";

        /// <summary>
        /// Amount of % to move the drill out when finished going down
        /// </summary>
        private static readonly float IncreaseDistance = 1.0f;

        private static readonly float PistonVelocity = 0.1f;
        private static readonly float PistonRetractVelocity = 1.5f;

        #region Variables

        private static bool _initPositions = false;
        private static bool _isDiggingDown = false;
        private static bool _isLiftingUp = false;
        private static bool _isExtending = false;
        private static bool _isForceStopped = false;
        private static float _horizontalPosition = 0f;

        private static IMyBlockGroup HorizontalPistonGroup;
        private static List<IMyExtendedPistonBase> HorizontalPistons;

        private static IMyBlockGroup VerticalPistonGroup;
        private static List<IMyExtendedPistonBase> VerticalPistons;

        private static IMyBlockGroup DrillGroup;
        private static List<IMyShipDrill> Drills;

        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        #endregion


        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            // Update every 100 Ticks
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void ResetDrill()
        {
            Echo("Resetting Drill Position");
            _initPositions = true;
            _isForceStopped = false;
        }

        public void ToggleDrills(bool enable)
        {
            string toggleText = enable ? "On" : "Off";

            Echo($"Setting Drills to {toggleText}");

            foreach (IMyShipDrill drill in Drills)
            {
                drill.Enabled = enable;
            }
        }

        public void RaiseVerticalPistons()
        {
            Echo("Retracting Vertical Pistons");
            foreach (IMyExtendedPistonBase verticalPiston in VerticalPistons)
            {
                verticalPiston.Velocity = PistonRetractVelocity;
                verticalPiston.Retract();
                verticalPiston.Enabled = true;
            }
        }

        public void LowerVerticalPistons()
        {
            Echo("Lowering Vertical Pistons");
            foreach (IMyExtendedPistonBase verticalPiston in VerticalPistons)
            {
                verticalPiston.Velocity = PistonVelocity;
                verticalPiston.Extend();
                verticalPiston.Enabled = true;
            }
        }

        public bool HasVerticalPistonsReachedMin()
        {

            int pistonCount = 0;
            foreach (IMyExtendedPistonBase verticalPiston in VerticalPistons)
            {
                if (verticalPiston.CurrentPosition != verticalPiston.LowestPosition) continue;
                pistonCount++;
            }
            Echo("All Vertical Pistons Retracted");
            if (pistonCount != VerticalPistons.Count) return false;
            return true;
        }

        public bool HasVerticalPistonsReachedMax()
        {
            int extendedVerticalCount = 0;
            foreach (IMyExtendedPistonBase verticalPiston in VerticalPistons)
            {
                Echo($"{verticalPiston.CurrentPosition} = {verticalPiston.LowestPosition} = {verticalPiston.HighestPosition}");
                if (verticalPiston.CurrentPosition != verticalPiston.HighestPosition) continue;
                extendedVerticalCount++;
            }
            Echo("All Pistons fully extended");
            if (extendedVerticalCount != VerticalPistons.Count) return false;
            return true;
        }

        public void RetractHorizontalPistons()
        {
            Echo("Retracting Horizontal Pistons");
            foreach (IMyExtendedPistonBase horizontalPiston in HorizontalPistons)
            {
                horizontalPiston.Velocity = PistonRetractVelocity;
                horizontalPiston.Retract();
                horizontalPiston.Enabled = true;
            }
        }

        public void ExtendHorizontalPistons()
        {
            Echo("Extending Arm");
            foreach (IMyExtendedPistonBase horizontalPiston in HorizontalPistons)
            {
                horizontalPiston.Velocity = PistonVelocity;
                horizontalPiston.Extend();
                horizontalPiston.MaxLimit = _horizontalPosition;
                horizontalPiston.Enabled = true;
            }
        }

        public void StopHorizontalPistons()
        {
            foreach (IMyExtendedPistonBase horizontalPiston in HorizontalPistons)
            {
                horizontalPiston.Velocity = 0f;
                horizontalPiston.Enabled = false;
            }
        }

        public bool HasHorizontalPistonsReachedMin()
        {

            int pistonCount = 0;
            foreach (IMyExtendedPistonBase horizontalPiston in HorizontalPistons)
            {
                if (horizontalPiston.CurrentPosition != horizontalPiston.LowestPosition) continue;
                pistonCount++;
            }
            Echo("All Horizontal Pistons Retracted");
            if (pistonCount != HorizontalPistons.Count) return false;
            return true;
        }

        public bool HasHorizontalPistonsReachedPosition()
        {
            int extendedHorizontalCount = 0;
            foreach (IMyExtendedPistonBase horizontalPiston in HorizontalPistons)
            {
                Echo($"{horizontalPiston.CurrentPosition} = {horizontalPiston.MaxLimit} = {horizontalPiston.HighestPosition}");
                if (horizontalPiston.CurrentPosition != horizontalPiston.MaxLimit) continue;
                extendedHorizontalCount++;
            }
            Echo("Arm Reached Position");

            if (extendedHorizontalCount != HorizontalPistons.Count) return false;
            return true;

        }

        public void TurnOffPistons()
        {
            foreach (IMyExtendedPistonBase horizontalPiston in HorizontalPistons)
            {
                horizontalPiston.Enabled = false;
            }

            foreach (IMyExtendedPistonBase verticalPiston in VerticalPistons)
            {
                verticalPiston.Enabled = false;
            }
            ToggleDrills(false);
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            if(argument.ToLower() == "reset")
            {
                ResetDrill();
                return;
            }

            if(argument.ToLower() == "stop")
            {
                TurnOffPistons();
                _isForceStopped = true;
            }

            if(argument.ToLower() == "start")
            {
                _isForceStopped = false;
            }

            if(argument.ToLower() == "raise")
            {
                _isForceStopped = true;
                RaiseVerticalPistons();
                ToggleDrills(false);
            }

            HorizontalPistonGroup = GridTerminalSystem.GetBlockGroupWithName(HorizontalGroupName);
            if(HorizontalPistonGroup == null)
            {
                Echo($"ERROR: Unable to find the {HorizontalGroupName} group!");
                return;
            }

            HorizontalPistons = new List<IMyExtendedPistonBase>();
            HorizontalPistonGroup.GetBlocksOfType<IMyExtendedPistonBase>(HorizontalPistons);
            if (!HorizontalPistons.Any())
            {
                Echo($"ERROR: Unable to find any Piston Tops in the {HorizontalGroupName} group!");
                return;
            }

            VerticalPistonGroup = GridTerminalSystem.GetBlockGroupWithName(VerticalGroupName);
            if(VerticalPistonGroup == null)
            {
                Echo($"ERROR: Unable to find the {VerticalGroupName} group!");
                return;
            }

            VerticalPistons = new List<IMyExtendedPistonBase>();
            VerticalPistonGroup.GetBlocksOfType<IMyExtendedPistonBase>(VerticalPistons);
            if (!VerticalPistons.Any())
            {

                Echo($"ERROR: Unable to find any Piston Tops in the {VerticalGroupName} group!");
                return;
            }

            DrillGroup = GridTerminalSystem.GetBlockGroupWithName(DrillGroupName);
            if(DrillGroup == null)
            {
                Echo($"ERROR: Unable to find the {DrillGroupName} group!");
                return;
            }

            Drills = new List<IMyShipDrill>();
            DrillGroup.GetBlocksOfType<IMyShipDrill>(Drills);
            if (!Drills.Any())
            {

                Echo($"ERROR: Unable to find any Drills in the {DrillGroupName} group!");
                return;
            }

            if (_isForceStopped) return;

            if (_initPositions)
            {
                Echo($"Resetting Position of Drill and Pistons");
                // Init all positions to 0. Stop Drill, Raise Vertical Sections, Retract Horizontal
                ToggleDrills(false);

                RaiseVerticalPistons();
                if (!HasVerticalPistonsReachedMin()) return;

                RetractHorizontalPistons();
                if (!HasHorizontalPistonsReachedMin()) return;

                _initPositions = false;
                _isDiggingDown = false;
                _isLiftingUp = false;
                _isExtending = false;
                _horizontalPosition = 0f;

                Echo("Drill position reset to 0");
                return;
            }

            if (!_isDiggingDown && !_isLiftingUp && !_isExtending || _isDiggingDown)
            {
                _isDiggingDown = true;

                ToggleDrills(true);

                LowerVerticalPistons();
                if (!HasVerticalPistonsReachedMax()) return;

                ToggleDrills(false);

                _isDiggingDown = false;
                _isLiftingUp = true;
            }

            if(_isLiftingUp && !_isDiggingDown && !_isExtending)
            {
                RaiseVerticalPistons();
                if (!HasVerticalPistonsReachedMin()) return;

                _isLiftingUp = false;
                _isExtending = true;
                _horizontalPosition += IncreaseDistance;
            }

            if(_isExtending && !_isDiggingDown && !_isLiftingUp)
            {
                ExtendHorizontalPistons();
                if (!HasHorizontalPistonsReachedPosition()) return;

                StopHorizontalPistons();

                _isExtending = false;
                _isDiggingDown = true;
            }
        }

        // Copy above this
    }

}
