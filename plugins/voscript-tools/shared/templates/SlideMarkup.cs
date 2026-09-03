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
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionCommandClass(HelpText = "Presses a markup button in the viewer so the user can draw an annotation. Trigger: the {{System}}MarkupButtons named list")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class SlideMarkup : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will press one of the markup buttons on the {{Vendor}} toolbar so the user can
            // then draw an annotation. The named list translates the spoken tool to its on-screen
            // name, so adding a tool is a named-list change, not a script change.
            //
            // *****

            string markupType = SpeechParams.TranslateSingle(SpeechParams.Names[0]);

            // ONLY if {{Vendor}} has annotation layers, so the command takes two spoken
            // parameters. Names[0] cannot tell them apart, so a multi-list command is the one case
            // that names its lists explicitly - and both must then be named, not just the second:
            //
            //   string markupType = SpeechParams.TranslateSingle("{{System}}MarkupButtons");
            //   string layer      = SpeechParams.TranslateSingle("{{System}}MarkupLayers");
            //
            // Delete the layer block below along with this comment if there are no layers.
            string layer = "";

            string titlePage = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                // Layer first, so the annotation the user is about to draw lands on it.
                // TODO: delete this block if {{Vendor}} has no annotation layers.
                if (!string.IsNullOrEmpty(layer))
                {
                    // TODO: confirm the layer picker's anchor. Anchor off a named neighbour -
                    // preferably one whose name carries its hotkey - rather than an index.
                    IUIAutomationElement layerAnchor = _Browser.FindElementOnPage(manager, "Negative (SHIFT + 7)", UIAControlType.Button);
                    IUIAutomationElement layerPicker = _Browser.GetNextSiblingElement(layerAnchor);
                    controller.Click(layerPicker);
                    WindowTools.Wait(250);

                    // The picker's entries are captioned by a child Text element; the parent
                    // takes the click.
                    IUIAutomationElement newLayer = _Browser.FindElementOnPage(manager, layer, UIAControlType.Text);
                    controller.Click(_Browser.GetParent(newLayer));
                    WindowTools.Wait(250);
                }

                // TODO: confirm the markup buttons are addressed by Name as Buttons, and that
                // the named list's translations match the live control names exactly.
                IUIAutomationElement button = _Browser.FindElementOnPage(manager, markupType, UIAControlType.Button);

                if (button == null)
                    throw new ClientException($"Could not find the \"{markupType}\" markup tool. Check the named list against the viewer toolbar.");

                controller.Click(button);
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
            }

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
