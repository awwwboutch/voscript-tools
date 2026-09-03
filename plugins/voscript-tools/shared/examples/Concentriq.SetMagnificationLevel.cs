// SOURCE: VOScript.Starter.Concentriq.SetMagnificationLevel@2P-3080-3.xml
// Published reference example, extracted 2026-09-01 from demo\sales.
// Read-only: do not edit. See examples/README.md for what each one demonstrates.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UIAutomationClient;           // For BrowserController
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using WindowsInput;                 // For InputSimulator
using WindowsInput.Native;          // For InputSimulator
#endregion USING NAMESPACES

namespace VOScript.Starter.Concentriq
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class SetMagnificationLevel : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 6/11/2025
            // Use:
            // This will
            //
            // *****
            
            string zoomMeasurement = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
            string titlePage = _Concentriq.FindCurrentTitlePage(Application);
            WindowTools.Instance.EnsureForegroundWindow(titlePage);
            
            // For sending keystrokes to the web page (or to any window)
            InputSimulator simulator = new InputSimulator();

            switch(zoomMeasurement)
            {
                case "In":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.ADD);
                    break;
                    
                case "Out":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.SUBTRACT);
                    break;
                    
                case "Reset":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_Z);
                    break;
                    
                case "0.1x":
                case "0.2x":
                case "0.5x":
                    var manager = new Browser.Manager.BrowserManager(titlePage);
                    var controller = manager.GetBrowserController();
                    try
                    {
                        IUIAutomationElement magValue = _Browser.FindElementOnPage(manager, "Zoom to " + zoomMeasurement, UIAControlType.Button);
                        if (!_Browser.IsVisiblyRendered(magValue))
                        {
                            IUIAutomationElement zoomButton = _Browser.FindElementOnPage(manager, "Zoom", UIAControlType.Button);
                            controller.Click(zoomButton);
                            System.Threading.Thread.Sleep(150);
                            
                            magValue = _Browser.FindElementOnPage(manager, "Zoom to " + zoomMeasurement, UIAControlType.Button);
                            controller.Click(magValue);                            
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusLog.WriteErrorEntry($"Error controlling Concentriq: {ex.Message}", ex);
                    }
                    finally
                    {
                        // Must dispose to avoid memory leaks and/or crashes
                        controller?.Dispose();
                    }
                    break;
                    
                case "1x":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_1);
                    break;
                    
                case "2x":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_2);
                    break;
                    
                case "4x":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_3);
                    break;
                    
                case "10x":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_4);
                    break;
                    
                case "20x":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_5);
                    break;
                    
                case "40x":
                    simulator.Keyboard.KeyPress(VirtualKeyCode.VK_6);
                    break;
                
                default:
                   return;
            }
        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE



