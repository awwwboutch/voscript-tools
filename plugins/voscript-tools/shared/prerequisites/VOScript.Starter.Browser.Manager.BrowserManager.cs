// PREREQUISITE: VOScript.Starter.Browser.Manager.BrowserManager
// Source revision 3P-2646-1, exported 2026-09-01 from demo\sales.
// Tier: core
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using UIAutomationClient;           // For BrowserController
using WindowsInput;                 // For InputSimulator
using WindowsInput.Native;          // For InputSimulator
using System.Runtime.InteropServices; // for cursor stuff
#endregion USING NAMESPACES

namespace VOScript.Starter.Browser.Manager
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION


    public class BrowserManager : IDisposable
    {
        private IBrowserController _controller;
        private IUIAutomationElement _document;
        private POINT _mousePosition;
        private bool _resetCursorPosition;
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
        
        //This allows the usage of mouse coordinates
        private struct POINT
        {
            public int X;
            public int Y;
        }
        
        
        /// <summary>
        /// Gets the current cursor position on the screen.
        /// </summary>
        /// <returns>Stores the starting mouse coordinates for returning later</returns>
        private POINT GetCurrentCursorPosition()
        {
            POINT point = new POINT();
            bool getPos = GetCursorPos(out point);
            if (getPos)
            {
                return point;
            }
            throw new InvalidOperationException("Unable to retrieve the cursor position.");
        }
        
        /// <summary>
    /// Sets the cursor position to specified screen coordinates.
    /// </summary>
    /// <param name="x">The X coordinate of the new cursor position.</param>
    /// <param name="y">The Y coordinate of the new cursor position.</param>
    private void SetCursorPosition()
    {
        if (!SetCursorPos(_mousePosition.X, _mousePosition.Y))
        {
            throw new InvalidOperationException("Unable to set the cursor position.");
        }
    }
        
        
        /// <summary>
        /// This will return the browser controller for use
        /// </summary>
        /// <returns></returns>
        public IBrowserController GetBrowserController()
        {
            if(_controller==null)
            {
                throw new InvalidOperationException("Browser has not been initialized!");
                
            }
            
            return _controller;
        }
        
        public IUIAutomationElement GetAndFocusBrowser()
        {
            if(_document==null)
            {
                throw new InvalidOperationException("Cannot find has not been initialized!");
            }
            
            return _document;
        }
        
        
        public BrowserManager()
        {
            _controller = BrowserController.GetInstance();
            
        }
        
        public BrowserManager (string PAGE_TITLE, bool resetCusorPosition = false)
        {
            _controller = BrowserController.GetInstance();
            _document = _controller.FindWebPageDocument(PAGE_TITLE) ??
            throw new ClientException($"Web page with title \"{PAGE_TITLE}\" not found.");
            _resetCursorPosition = resetCusorPosition;
            
            _mousePosition = _resetCursorPosition ? GetCurrentCursorPosition() : new POINT();
        }
        
        void IDisposable.Dispose()
        {
            //This will move the mouse back to the starting location when the command is finished
            if (_resetCursorPosition)
                SetCursorPosition();
            StatusLog.WriteInformationEntry("Disposing of the controller");
              _controller?.Dispose(); //Dispose of the controller
        }


            
             // IBrowserController controller = null;
                
        
              //      StatusLog.WriteInformationEntry("here");
            //        // Access the web page
             //       const string PAGE_TITLE = "NovoPath 360";
             //       controller = BrowserController.GetInstance();


#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE


