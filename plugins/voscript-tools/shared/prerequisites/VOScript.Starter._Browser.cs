// PREREQUISITE: VOScript.Starter._Browser
// Source revision 13P-3518-5, exported 2026-09-01 from demo\sales.
// Tier: core
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using UIAutomationClient;           // CUIAutomation / IUIAutomationElement (this IS UIA3)
using WindowsInput;                 // For InputSimulator
using WindowsInput.Native;          // For InputSimulator
using BrowserManager = VOScript.Starter.Browser.Manager.BrowserManager;

// NOTE: 'using System.Windows.Automation;' was removed. Nothing in this file used it --
// every enum referenced below (TreeScope, ToggleState, ExpandCollapseState,
// WindowVisualState, ScrollAmount) is the UIAutomationClient COM version. Keeping both
// namespaces imported risks CS0104 ambiguity on all five of those names the moment
// someone adds a reference that fully resolves the managed wrapper.
#endregion USING NAMESPACES

namespace VOScript.Starter
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _Browser : ExtensionScript
    {
        #region SHARED AUTOMATION CORE

        // Previously there were two static instances (_automation and automation) plus
        // roughly twenty 'new CUIAutomation()' calls scattered through the file. Each
        // instantiation creates a separate COM object with its own provider cache.
        // One shared instance for the whole library.
        private static readonly CUIAutomation _uia = new CUIAutomation();

        /// <summary>
        /// Shared CUIAutomation instance. Exposed so sibling libraries (_Desktop) and
        /// calling scripts can reuse it rather than newing up their own.
        /// </summary>
        public static CUIAutomation UIA
        {
            get { return _uia; }
        }

        #endregion SHARED AUTOMATION CORE

        #region CONDITION BUILDERS

        // Every Find* method in the original file rebuilt these inline. Centralizing them
        // fixes an inconsistency that mattered: some call sites cast the UIAControlType to
        // int before handing it to CreatePropertyCondition and some did not. Since the
        // parameter is 'object', an uncast enum gets boxed and marshaled as a VARIANT,
        // which only matches if it lands as VT_I4. The explicit cast removes the question.

        /// <summary>Condition matching an exact Name.</summary>
        public static IUIAutomationCondition ByName(string name)
        {
            return _uia.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, name);
        }

        /// <summary>Condition matching an exact AutomationId.</summary>
        public static IUIAutomationCondition ById(string automationId)
        {
            return _uia.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, automationId);
        }

        /// <summary>Condition matching a ControlType.</summary>
        public static IUIAutomationCondition ByControlType(UIAControlType controlType)
        {
            return _uia.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ControlTypePropertyId, (int)controlType);
        }

        /// <summary>Condition matching a raw UIA control type id.</summary>
        public static IUIAutomationCondition ByControlType(int controlTypeId)
        {
            return _uia.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ControlTypePropertyId, controlTypeId);
        }

        /// <summary>Condition matching AutomationId AND ControlType.</summary>
        public static IUIAutomationCondition ByIdAndType(string automationId, UIAControlType controlType)
        {
            return _uia.CreateAndCondition(ById(automationId), ByControlType(controlType));
        }

        /// <summary>Condition matching Name AND ControlType.</summary>
        public static IUIAutomationCondition ByNameAndType(string name, UIAControlType controlType)
        {
            return _uia.CreateAndCondition(ByName(name), ByControlType(controlType));
        }

        /// <summary>Matches anything. Useful as a default when enumerating children.</summary>
        public static IUIAutomationCondition Anything()
        {
            return _uia.CreateTrueCondition();
        }

        #endregion CONDITION BUILDERS

        #region FIND: MANAGER-SCOPED

        /// <summary>
        /// Finds the first element on the page matching Name AND ControlType.
        /// </summary>
        public static IUIAutomationElement FindElementOnPage(
            BrowserManager browserManager,
            string name,
            UIAControlType controlType)
        {
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();
            return webPage.FindFirst(TreeScope.TreeScope_Descendants, ByNameAndType(name, controlType));
        }

        /// <summary>
        /// Finds the first element on the page by AutomationId, optionally constrained
        /// by ControlType.
        /// </summary>
        public static IUIAutomationElement FindElementByAutomationId(
            BrowserManager browserManager,
            string automationId,
            UIAControlType? controlType = null)
        {
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();
            return FindElementByAutomationId(webPage, automationId, controlType);
        }

        /// <summary>
        /// Scoped overload: searches beneath an arbitrary root. Prefer this whenever the
        /// AutomationId is only unique within a component.
        /// </summary>
        public static IUIAutomationElement FindElementByAutomationId(
            IUIAutomationElement root,
            string automationId,
            UIAControlType? controlType = null)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (string.IsNullOrWhiteSpace(automationId))
                throw new ArgumentException("AutomationId cannot be null or empty.", nameof(automationId));

            IUIAutomationCondition condition = controlType.HasValue
                ? ByIdAndType(automationId, controlType.Value)
                : ById(automationId);

            return root.FindFirst(TreeScope.TreeScope_Descendants, condition);
        }

        /// <summary>
        /// Walks a chain of AutomationIds, narrowing scope at each hop. The most reliable
        /// way to address a deeply nested element without depending on global uniqueness.
        /// </summary>
        public static IUIAutomationElement FindByPath(IUIAutomationElement root, params string[] automationIds)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (automationIds == null || automationIds.Length == 0)
                return root;

            IUIAutomationElement current = root;

            foreach (string automationId in automationIds)
            {
                IUIAutomationElement next = current.FindFirst(
                    TreeScope.TreeScope_Descendants, ById(automationId));

                if (next == null)
                {
                    StatusLog.WriteErrorEntry($"FindByPath: broke at '{automationId}'.");
                    return null;
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Returns true if a named element exists on the page.
        ///
        /// Renamed from IsElementVisible, which was misleading -- it only ever tested
        /// presence in the tree, never visibility. IsElementVisible is kept below as a
        /// wrapper so existing scripts keep working; use IsVisiblyRendered if you need
        /// an actual visibility check.
        /// </summary>
        public static bool IsElementPresent(BrowserManager browserManager, string elementName)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            IUIAutomationElement element = browserController.FindElementByName(webPage, elementName);

            if (element == null)
            {
                StatusLog.WriteInformationEntry("Element is not present");
                return false;
            }

            StatusLog.WriteInformationEntry("Present");
            return true;
        }

        /// <summary>
        /// Back-compat wrapper for IsElementPresent. Prefer IsElementPresent for clarity.
        /// </summary>
        public static bool IsElementVisible(BrowserManager browserManager, string elementName)
        {
            return IsElementPresent(browserManager, elementName);
        }

        /// <summary>
        /// True only if the element is on screen, enabled, and has real geometry.
        /// </summary>
        public static bool IsVisiblyRendered(IUIAutomationElement element)
        {
            if (element == null)
                return false;

            try
            {
                if (element.CurrentIsOffscreen != 0)
                    return false;

                tagRECT r = element.CurrentBoundingRectangle;
                if (r.right <= r.left || r.bottom <= r.top)
                    return false;

                if (element.CurrentIsEnabled == 0)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion FIND: MANAGER-SCOPED

        #region FIND: ELEMENT-SCOPED

        // The original file had five "nth" finders split across two indexing conventions:
        //   1-based: GetNthChild, GetNthChildByType, GetNthChildByAutomationIdAndControlType
        //   0-based: GetNthElementByControlType, GetNthElementByAutomationIdAndControlType
        // All five now delegate to one core and keep their original convention, so no
        // existing call site changes meaning. See FindNth for the canonical 0-based form.

        /// Canonical nth-match finder. ZERO-BASED. Private on purpose -- every public
        /// nth-finder in this file is 1-based and translates on the way in.
        /// </summary>
        private static IUIAutomationElement FindNth(
            IUIAutomationElement root,
            int index,
            IUIAutomationCondition condition = null,
            TreeScope scope = TreeScope.TreeScope_Descendants)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Index must be >= 0.");

            IUIAutomationElementArray matches = root.FindAll(scope, condition ?? Anything());

            if (matches == null || matches.Length <= index)
                return null;

            return matches.GetElement(index);
        }

        /// <summary>
        /// Finds the nth descendant matching an optional condition. ONE-BASED.
        /// </summary>
        public static IUIAutomationElement GetNthChild(
            IUIAutomationElement parent,
            int n,
            IUIAutomationCondition condition = null)
        {
            if (parent == null || n < 1)
                return null;

            return FindNth(parent, n - 1, condition);
        }

        /// <summary>
        /// Finds the nth descendant with the given ControlType. ONE-BASED.
        /// Alias of GetNthElementByControlType, retained for existing call sites.
        /// </summary>
        public static IUIAutomationElement GetNthChildByType(
            IUIAutomationElement parent,
            UIAControlType controlType,
            int n)
        {
            if (parent == null || n < 1)
                return null;
        
            return GetNthElementByControlType(parent, controlType, n);
        }

        /// <summary>
        /// Finds the nth descendant matching AutomationId AND ControlType. ONE-BASED.
        /// </summary>
        public static IUIAutomationElement GetNthChildByAutomationIdAndControlType(
            IUIAutomationElement parent,
            string automationId,
            UIAControlType controlType,
            int n)
        {
            if (parent == null || string.IsNullOrEmpty(automationId) || n < 1)
                return null;

            return FindNth(parent, n - 1, ByIdAndType(automationId, controlType));
        }

        /// <summary>
        /// Finds the nth element with the specified ControlType. ONE-BASED.
        /// </summary>
        /// <param name="root">Subtree root to search.</param>
        /// <param name="controlType">ControlType to match.</param>
        /// <param name="n">1-based position (1 = first match).</param>
        /// <param name="scope">Tree scope. Defaults to Descendants.</param>
        public static IUIAutomationElement GetNthElementByControlType(
            IUIAutomationElement root,
            UIAControlType controlType,
            int n,
            TreeScope scope = TreeScope.TreeScope_Descendants)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
        
            if (n < 1)
                throw new ArgumentOutOfRangeException(nameof(n), "n must be at least 1.");
        
            return FindNth(root, n - 1, ByControlType(controlType), scope);
        }

        /// <summary>
        /// Finds the nth element matching AutomationId AND ControlType. ZERO-BASED.
        /// </summary>
        public static IUIAutomationElement GetNthElementByAutomationIdAndControlType(
            IUIAutomationElement root,
            string automationId,
            UIAControlType controlType,
            int index)
        {
            if (string.IsNullOrWhiteSpace(automationId))
                throw new ArgumentException("AutomationId cannot be null or empty.", nameof(automationId));

            return FindNth(root, index, ByIdAndType(automationId, controlType));
        }

        // The original had three near-identical id+type finders differing only in
        // parameter type and doc comment: GetChildByAutomationIdAndControlType (int
        // overload), GetChildByAutomationIdAndControlType (UIAControlType overload), and
        // GetElementByAutomationIdAndControlType. All three searched Descendants despite
        // the "Child" naming. They now delegate to FindElementByAutomationId.

        /// <summary>
        /// Finds a descendant matching AutomationId AND ControlType.
        /// </summary>
        public static IUIAutomationElement GetChildByAutomationIdAndControlType(
            IUIAutomationElement parent,
            string automationId,
            UIAControlType controlType)
        {
            if (parent == null || string.IsNullOrEmpty(automationId))
                return null;

            return parent.FindFirst(TreeScope.TreeScope_Descendants, ByIdAndType(automationId, controlType));
        }

        /// <summary>
        /// Raw control-type-id overload, for callers holding a UIA_ControlTypeIds constant.
        /// </summary>
        public static IUIAutomationElement GetChildByAutomationIdAndControlType(
            IUIAutomationElement parent,
            string automationId,
            int controlTypeId)
        {
            if (parent == null || string.IsNullOrEmpty(automationId))
                return null;

            IUIAutomationCondition condition = _uia.CreateAndCondition(
                ById(automationId), ByControlType(controlTypeId));

            return parent.FindFirst(TreeScope.TreeScope_Descendants, condition);
        }

        /// <summary>
        /// Alias of GetChildByAutomationIdAndControlType, kept for existing call sites.
        /// Throws on null/empty input rather than returning null.
        /// </summary>
        public static IUIAutomationElement GetElementByAutomationIdAndControlType(
            IUIAutomationElement document,
            string automationId,
            UIAControlType controlType)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrWhiteSpace(automationId))
                throw new ArgumentException("AutomationId cannot be null or empty.", nameof(automationId));

            return document.FindFirst(TreeScope.TreeScope_Descendants, ByIdAndType(automationId, controlType));
        }

        /// <summary>
        /// Finds a DIRECT CHILD by exact Name. Unlike the methods above, this one really
        /// does scope to Children.
        /// </summary>
        public static IUIAutomationElement GetChildByName(IUIAutomationElement parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            return parent.FindFirst(TreeScope.TreeScope_Children, ByName(name));
        }

        #endregion FIND: ELEMENT-SCOPED

        #region TREE NAVIGATION

        /// <summary>Gets the parent of the given element.</summary>
        public static IUIAutomationElement GetParent(IUIAutomationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            return _uia.ControlViewWalker.GetParentElement(element);
        }

        /// <summary>Gets the first child of the given element.</summary>
        public static IUIAutomationElement GetChild(IUIAutomationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            return _uia.ControlViewWalker.GetFirstChildElement(element);
        }

        /// <summary>Gets the Nth next sibling. Defaults to the immediate next sibling.</summary>
        public static IUIAutomationElement GetNextSiblingElement(IUIAutomationElement element, int n = 1)
        {
            return WalkSiblings(element, n, forward: true);
        }

        /// <summary>Gets the Nth previous sibling. Defaults to the immediate previous sibling.</summary>
        public static IUIAutomationElement GetPreviousSiblingElement(IUIAutomationElement element, int n = 1)
        {
            return WalkSiblings(element, n, forward: false);
        }

        private static IUIAutomationElement WalkSiblings(IUIAutomationElement element, int n, bool forward)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (n < 1)
                throw new ArgumentOutOfRangeException(nameof(n), "n must be at least 1.");

            IUIAutomationTreeWalker walker = _uia.ControlViewWalker;
            IUIAutomationElement current = element;

            for (int i = 0; i < n; i++)
            {
                current = forward
                    ? walker.GetNextSiblingElement(current)
                    : walker.GetPreviousSiblingElement(current);

                if (current == null)
                    break;
            }

            return current;
        }

        /// <summary>
        /// Returns the last sibling under the element's parent, regardless of type.
        /// </summary>
        public static IUIAutomationElement GetLastSiblingElement(IUIAutomationElement element)
        {
            return GetLastSiblingOfType(element, null);
        }

        /// <summary>
        /// Returns the last sibling under the element's parent whose ControlType matches.
        /// Safer than indexing, because Chromium regularly inserts hidden helper elements.
        /// Pass null for desiredType to accept any type.
        /// </summary>
        public static IUIAutomationElement GetLastSiblingOfType(
            IUIAutomationElement referenceElement,
            UIAControlType? desiredType)
        {
            if (referenceElement == null)
                throw new ArgumentNullException(nameof(referenceElement));

            IUIAutomationTreeWalker walker = _uia.ControlViewWalker;

            IUIAutomationElement parent = walker.GetParentElement(referenceElement);
            if (parent == null)
                return null;

            IUIAutomationElement current = walker.GetFirstChildElement(parent);
            if (current == null)
                return null;

            IUIAutomationElement lastMatch = null;

            while (current != null)
            {
                try
                {
                    if (!desiredType.HasValue || current.CurrentControlType == (int)desiredType.Value)
                        lastMatch = current;
                }
                catch (COMException)
                {
                    // Stale COM object -- skip it.
                }

                current = walker.GetNextSiblingElement(current);
            }

            return lastMatch;
        }

        #endregion TREE NAVIGATION

        #region WAIT

        /// <summary>
        /// Internal wait loop shared by every WaitFor* overload.
        /// </summary>
        private static bool WaitForElementCore(Func<IUIAutomationElement> findElement, int timeoutMilliseconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                IUIAutomationElement element = null;

                try
                {
                    element = findElement();
                }
                catch (COMException)
                {
                    // Provider busy, or the element was replaced mid-search. Retry.
                }

                if (element != null && element.CurrentIsOffscreen == 0 && element.CurrentIsEnabled == 1)
                {
                    StatusLog.WriteInformationEntry("Element is visible and enabled!");
                    return true;
                }

                WindowTools.Instance.Wait(500);
                StatusLog.WriteInformationEntry("Element is not yet found or ready.");
            }

            stopwatch.Stop();
            StatusLog.WriteInformationEntry("Timeout reached. Element is not visible or enabled.");
            return false;
        }

        /// <summary>
        /// Waits for the nth element of a given ControlType to become visible and enabled.
        /// </summary>
        public static bool WaitForElement(
            BrowserManager browserManager,
            UIAControlType controlType,
            int elementPosition,
            int timeoutMilliseconds = 10000)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            return WaitForElementCore(
                () => browserController.FindNthElementOfType(webPage, controlType, elementPosition),
                timeoutMilliseconds);
        }

        /// <summary>
        /// Waits for a named element to become visible and enabled.
        /// </summary>
        public static bool WaitForElement(
            BrowserManager browserManager,
            string elementName,
            int timeoutMilliseconds = 10000)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            return WaitForElementCore(
                () => browserController.FindElementByName(webPage, elementName),
                timeoutMilliseconds);
        }

        /// <summary>
        /// Waits for a named element of a specific ControlType. More precise than the
        /// name-only overload -- avoids false positives on shared names.
        /// </summary>
        public static bool WaitForElement(
            BrowserManager browserManager,
            string elementName,
            UIAControlType controlType,
            int timeoutMilliseconds = 10000)
        {
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            return WaitForElementCore(
                () => webPage.FindFirst(TreeScope.TreeScope_Descendants, ByNameAndType(elementName, controlType)),
                timeoutMilliseconds);
        }

        /// <summary>
        /// Waits for an element by AutomationId, optionally constrained by ControlType.
        /// </summary>
        public static bool WaitForElementByAutomationId(
            BrowserManager browserManager,
            string automationId,
            UIAControlType? controlType = null,
            int timeoutMilliseconds = 10000)
        {
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            return WaitForElementCore(
                () => FindElementByAutomationId(webPage, automationId, controlType),
                timeoutMilliseconds);
        }

        #endregion WAIT

        #region CLICK AND INVOKE

        /// <summary>
        /// Activates an element using the safest available mechanism, in order:
        ///   1. InvokePattern         -- no mouse movement, DPI and monitor agnostic
        ///   2. LegacyIAccessible     -- DoDefaultAction, for partial providers
        ///   3. SelectionItemPattern  -- list items, tabs, tree items
        ///   4. Physical click        -- last resort
        /// </summary>
        public static bool Invoke(IUIAutomationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (element.CurrentIsEnabled == 0)
            {
                StatusLog.WriteWarningEntry("Invoke: element is disabled.");
                return false;
            }

            ScrollIntoViewIfPossible(element);

            try
            {
                IUIAutomationInvokePattern invoke =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)
                    as IUIAutomationInvokePattern;

                if (invoke != null)
                {
                    invoke.Invoke();
                    return true;
                }
            }
            catch (COMException) { }

            try
            {
                IUIAutomationLegacyIAccessiblePattern legacy =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_LegacyIAccessiblePatternId)
                    as IUIAutomationLegacyIAccessiblePattern;

                if (legacy != null)
                {
                    legacy.DoDefaultAction();
                    return true;
                }
            }
            catch (COMException) { }

            try
            {
                IUIAutomationSelectionItemPattern selection =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId)
                    as IUIAutomationSelectionItemPattern;

                if (selection != null)
                {
                    selection.Select();
                    return true;
                }
            }
            catch (COMException) { }

            try
            {
                StatusLog.WriteWarningEntry("Invoke: no usable pattern; falling back to a coordinate click.");
                ClickElement(element);
                return true;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Invoke: all strategies failed. {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds an element by Name and ControlType and clicks it.
        /// </summary>
        public static void ClickElementOnPage(
            BrowserManager browserManager,
            UIAControlType controlType,
            string buttonName)
        {
            IUIAutomationElement buttonToClick = FindElementOnPage(browserManager, buttonName, controlType);

            if (buttonToClick != null)
            {
                IBrowserController browserController = browserManager.GetBrowserController();
                StatusLog.WriteInformationEntry($"Clicking button '{buttonName}'.");
                browserController.Click(buttonToClick);
            }
            else
            {
                StatusLog.WriteErrorEntry($"Could not find button '{buttonName}' on the current page.");
            }
        }

        /// <summary>
        /// Finds an element by AutomationId and ControlType and activates it, retrying
        /// until the timeout expires.
        /// </summary>
        public static bool ClickElementByAutomationId(
            BrowserManager browserManager,
            string automationId,
            UIAControlType? controlType = null,
            int timeoutMs = 3000)
        {
            IUIAutomationElement root = browserManager.GetAndFocusBrowser();
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                IUIAutomationElement element = null;

                try
                {
                    element = FindElementByAutomationId(root, automationId, controlType);
                }
                catch (COMException) { }

                if (element != null && element.CurrentIsEnabled == 1)
                {
                    StatusLog.WriteInformationEntry($"Activating element '{automationId}'.");

                    if (Invoke(element))
                        return true;

                    // Invoke exhausted its own chain; give the controller a turn.
                    try
                    {
                        browserManager.GetBrowserController().Click(element);
                        return true;
                    }
                    catch { }
                }

                Thread.Sleep(100);
            }

            stopwatch.Stop();
            StatusLog.WriteErrorEntry(
                $"ClickElementByAutomationId: '{automationId}' not found or never became enabled within {timeoutMs}ms.");
            return false;
        }

        /// <summary>
        /// Back-compat wrapper: Button is the assumed ControlType.
        /// </summary>
        public static bool ClickButtonByAutomationId(
            BrowserManager browserManager,
            string automationId,
            int timeoutMs = 3000)
        {
            return ClickElementByAutomationId(
                browserManager, automationId, UIAControlType.Button, timeoutMs);
        }

        /// <summary>
        /// Back-compat wrapper accepting a raw control type id.
        /// </summary>
        public static bool ClickElementByAutomationId(
            BrowserManager browserManager,
            int controlType,
            string automationId,
            int timeoutMs = 3000)
        {
            return ClickElementByAutomationId(
                browserManager, automationId, (UIAControlType)controlType, timeoutMs);
        }

        /// <summary>
        /// Finds an element by AutomationId and ControlType and clicks it. Returns false
        /// rather than throwing when the element is absent.
        /// </summary>
        public static bool ClickElementByAutomationIdAndControlType(
            BrowserManager browserManager,
            string automationId,
            UIAControlType controlType,
            int timeoutMs = 3000)
        {
            return ClickElementByAutomationId(browserManager, automationId, controlType, timeoutMs);
        }

        /// <summary>
        /// Clicks the nth element of a given ControlType on the page.
        ///
        /// PressUIElementOnSlidePage was a byte-identical copy of this method and now
        /// forwards to it.
        /// </summary>
        public static void PressElementOnPage(
            BrowserManager browserManager,
            UIAControlType controlType,
            int nthPosition)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();
            IUIAutomationElement button = browserController.FindNthElementOfType(webPage, controlType, nthPosition);

            if (button == null)
            {
                StatusLog.WriteErrorEntry(
                    $"PressElementOnPage: no {controlType} at position {nthPosition}.");
                return;
            }

            browserController.Click(button);
        }

        /// <summary>Back-compat alias of PressElementOnPage.</summary>
        public static void PressUIElementOnSlidePage(
            BrowserManager browserManager,
            UIAControlType controlType,
            int nthPosition)
        {
            PressElementOnPage(browserManager, controlType, nthPosition);
        }

        /// <summary>
        /// Clicks an element by Name.
        /// </summary>
        public static void PressUIElementByName(BrowserManager browserManager, string name)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();
            IUIAutomationElement element = browserController.FindElementByName(webPage, name);

            if (element == null)
            {
                StatusLog.WriteErrorEntry($"PressUIElementByName: '{name}' not found.");
                return;
            }

            browserController.Click(element);
        }

        /// <summary>
        /// Opens a picker via its toggle, waits for a named item, then clicks it. For
        /// dropdowns whose items are absent from the tree until the picker is open.
        /// </summary>
        public static bool ClickPickerItem(
            BrowserManager browserManager,
            string toggleAutomationId,
            UIAControlType toggleControlType,
            string itemName,
            UIAControlType itemControlType,
            int timeoutMs = 5000)
        {
            bool toggled = ClickElementByAutomationId(
                browserManager, toggleAutomationId, toggleControlType);

            if (!toggled)
            {
                StatusLog.WriteErrorEntry(
                    $"ClickPickerItem: Failed to click toggle (AutomationId='{toggleAutomationId}').");
                return false;
            }

            bool itemReady = WaitForElement(browserManager, itemName, itemControlType, timeoutMs);

            if (!itemReady)
            {
                StatusLog.WriteErrorEntry(
                    $"ClickPickerItem: Timed out waiting for picker item '{itemName}' ({itemControlType}).");
                return false;
            }

            ClickElementOnPage(browserManager, itemControlType, itemName);

            StatusLog.WriteInformationEntry($"ClickPickerItem: Successfully selected '{itemName}'.");
            return true;
        }

        #endregion CLICK AND INVOKE

        #region PHYSICAL INPUT

        /// <summary>
        /// Physical left click at the centre of the element.
        ///
        /// CHANGED: normalization now uses virtual-desktop metrics instead of
        /// SM_CXSCREEN/SM_CYSCREEN. The old version only addressed the primary monitor,
        /// so any element on a secondary display received its click at the wrong
        /// coordinates. ControlClick was already doing this correctly.
        /// </summary>
        public static void ClickElement(IUIAutomationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            tagPOINT point = GetClickPoint(element);

            INPUT[] inputs = new INPUT[3];

            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = (int)NormalizeX(point.x),
                    dy = (int)NormalizeY(point.y),
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN }
            };

            inputs[2] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            // Give the renderer time to place the caret.
            Thread.Sleep(40);
        }

        /// <summary>
        /// Physical double click at the element's click point.
        /// </summary>
        public static void DoubleClickElement(IUIAutomationElement element, int? interClickDelayMs = null)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            tagPOINT point = GetClickPoint(element);

            SetCursorPos(point.x, point.y);
            Thread.Sleep(30);

            int delay = interClickDelayMs ?? Math.Min(120, (int)GetDoubleClickTime() / 3);

            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(delay);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Ctrl+click, for multi-select lists.
        /// </summary>
        public static void ControlClick(IUIAutomationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            ScrollIntoViewIfPossible(element);
            tagPOINT point = GetClickPoint(element);

            InputSimulator simulator = new InputSimulator();

            simulator.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
            simulator.Mouse.MoveMouseToPositionOnVirtualDesktop(NormalizeX(point.x), NormalizeY(point.y));
            simulator.Mouse.LeftButtonClick();
            simulator.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
        }

        /// <summary>
        /// Resolves a screen point for an element: the provider's clickable point where
        /// one is offered, otherwise the centre of the bounding rectangle.
        ///
        /// CHANGED: the success test is now '!= 0'. GetClickablePoint's return value is
        /// the gotClickable BOOL from the COM retval, not an HRESULT -- HRESULT failures
        /// surface as exceptions. The previous '== 0' test (commented "0 == S_OK") took
        /// the clickable-point branch precisely when no point was available.
        /// </summary>
        public static tagPOINT GetClickPoint(IUIAutomationElement element)
        {
            tagPOINT point;

            try
            {
                if (element.GetClickablePoint(out point) != 0)
                    return point;
            }
            catch (COMException) { }

            tagRECT r = element.CurrentBoundingRectangle;

            if (r.right <= r.left || r.bottom <= r.top)
                throw new InvalidOperationException(
                    "Element has no clickable point and an invalid bounding rectangle. " +
                    "Scroll it into view first.");

            point = new tagPOINT
            {
                x = r.left + (r.right - r.left) / 2,
                y = r.top + (r.bottom - r.top) / 2
            };

            return point;
        }

        /// <summary>
        /// Sends a hotkey to whatever currently has focus.
        ///
        /// CHANGED: the 'wait' parameter is now honored. It was previously accepted and
        /// silently ignored, so any script passing a longer delay was not getting one.
        /// </summary>
        public static void Send(VirtualKeyCode virtualKeyCode, int wait = 10)
        {
            new InputSimulator().Keyboard.KeyPress(virtualKeyCode);

            if (wait > 0)
                Thread.Sleep(wait);
        }

        /// <summary>
        /// Sends a modified hotkey, e.g. Send(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_S).
        /// </summary>
        public static void Send(VirtualKeyCode modifier, VirtualKeyCode key, int wait = 10)
        {
            new InputSimulator().Keyboard.ModifiedKeyStroke(modifier, key);

            if (wait > 0)
                Thread.Sleep(wait);
        }

        #endregion PHYSICAL INPUT

        #region FOCUS

        /// <summary>Sets focus on a specific element via the controller.</summary>
        public static void FocusElement(IBrowserController controller, IUIAutomationElement element)
        {
            controller.SetFocus(element);
        }

        /// <summary>Sets focus on an element found by Name.</summary>
        public static void FocusElementByName(BrowserManager browserManager, string elementName)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();
            IUIAutomationElement element = browserController.FindElementByName(webPage, elementName);

            if (element == null)
            {
                StatusLog.WriteErrorEntry($"FocusElementByName: '{elementName}' not found.");
                return;
            }

            browserController.SetFocus(element);
        }

        /// <summary>Sets focus on the nth element of a given ControlType.</summary>
        public static void FocusElementByNthElement(
            BrowserManager browserManager,
            UIAControlType controlType,
            int nthPosition)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();
            IUIAutomationElement element = browserController.FindNthElementOfType(webPage, controlType, nthPosition);

            if (element == null)
            {
                StatusLog.WriteErrorEntry(
                    $"FocusElementByNthElement: no {controlType} at position {nthPosition}.");
                return;
            }

            browserController.SetFocus(element);
        }

        /// <summary>Back-compat wrapper: Button is the assumed ControlType.</summary>
        public static void FocusButtonByNthElement(BrowserManager browserManager, int nthPosition)
        {
            FocusElementByNthElement(browserManager, UIAControlType.Button, nthPosition);
        }

        /// <summary>
        /// Reads the focused element, scrolls it into view, and drives it to an "on"
        /// state -- Toggle where available, then SelectionItem, then Invoke.
        /// </summary>
        public static IUIAutomationElement GetFocusedElementAndEnsureOn(int settleMs = 75)
        {
            if (settleMs > 0)
                Thread.Sleep(settleMs);

            IUIAutomationElement element = _uia.GetFocusedElement();

            if (element == null)
                return null;

            ScrollIntoViewIfPossible(element);

            // Toggle: up to three attempts handles Indeterminate -> Off -> On.
            try
            {
                IUIAutomationTogglePattern toggle =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId)
                    as IUIAutomationTogglePattern;

                if (toggle != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (toggle.CurrentToggleState == ToggleState.ToggleState_On)
                            break;

                        toggle.Toggle();
                        Thread.Sleep(20);
                    }

                    return element;
                }
            }
            catch (COMException) { }

            try
            {
                IUIAutomationSelectionItemPattern selection =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId)
                    as IUIAutomationSelectionItemPattern;

                if (selection != null)
                {
                    if (selection.CurrentIsSelected == 0)
                        selection.Select();

                    return element;
                }
            }
            catch (COMException) { }

            try
            {
                IUIAutomationInvokePattern invoke =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)
                    as IUIAutomationInvokePattern;

                if (invoke != null)
                    invoke.Invoke();
            }
            catch (COMException) { }

            return element;
        }

        #endregion FOCUS

        #region TEXT AND VALUE

        /// <summary>
        /// Sets an element's text via ValuePattern, falling back to focus plus clipboard
        /// paste when the provider ignores or rejects SetValue.
        ///
        /// TWO CHANGES worth reviewing:
        ///  1. The trailing Backspace keypress was removed. After Ctrl+A then Ctrl+V the
        ///     caret sits at the end of the pasted text, so the Backspace deleted its last
        ///     character. If some field genuinely needed it (dismissing an autocomplete
        ///     dropdown, for instance), pass dismissAutoComplete: true to restore it.
        ///  2. ClipSet now runs only on the fallback path. It previously ran first thing
        ///     on every call, overwriting the user's clipboard even when ValuePattern
        ///     succeeded and the clipboard was never needed.
        /// </summary>
        public static void SetElementText(
            IUIAutomationElement element,
            string text,
            bool dismissAutoComplete = false)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // 1) ValuePattern
            try
            {
                IUIAutomationValuePattern valuePattern =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)
                    as IUIAutomationValuePattern;

                if (valuePattern != null && valuePattern.CurrentIsReadOnly == 0)
                {
                    valuePattern.SetValue(text);
                    Thread.Sleep(400);

                    // Read the value back -- Chrome will accept SetValue and discard it.
                    if (Normalize(valuePattern.CurrentValue) == Normalize(text))
                        return;
                }
            }
            catch (COMException) { }

            StatusLog.WriteWarningEntry(
                "Failed to set text via UIAutomation. Attempting to use the clipboard instead.");

            // 2) Real-user keyboard editing
            Automation.ClipSet(text);

            try { element.SetFocus(); }
            catch (COMException) { ClickElement(element); }

            Thread.Sleep(150);

            InputSimulator simulator = new InputSimulator();

            simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_A);
            Thread.Sleep(50);
            simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
            Thread.Sleep(50);

            if (dismissAutoComplete)
                simulator.Keyboard.KeyPress(VirtualKeyCode.BACK);
        }

        /// <summary>
        /// Returns TextPattern.DocumentRange text if supported; otherwise Name.
        /// </summary>
        public static string GetElementText(IUIAutomationElement element)
        {
            if (element == null)
                return null;

            try
            {
                IUIAutomationTextPattern textPattern =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId)
                    as IUIAutomationTextPattern;

                if (textPattern != null)
                {
                    string text = textPattern.DocumentRange.GetText(-1);
                    return text?.Trim();
                }
            }
            catch (COMException) { }

            return SafeGet(() => element.CurrentName, string.Empty)?.Trim();
        }

        /// <summary>
        /// Finds the first DIRECT CHILD of the parent whose text matches a regex.
        /// Checks TextPattern first, then falls back to Name.
        /// </summary>
        public static IUIAutomationElement GetSiblingMatchingRegex(
            IUIAutomationElement parent,
            string pattern,
            RegexOptions options = RegexOptions.IgnoreCase)
        {
            return FindMatchingRegex(parent, pattern, TreeScope.TreeScope_Children, options);
        }

        /// <summary>
        /// Finds the first DESCENDANT of the root whose text matches a regex.
        /// </summary>
        public static IUIAutomationElement GetElementMatchingRegex(
            IUIAutomationElement root,
            string pattern,
            RegexOptions options = RegexOptions.IgnoreCase)
        {
            return FindMatchingRegex(root, pattern, TreeScope.TreeScope_Descendants, options);
        }

        private static IUIAutomationElement FindMatchingRegex(
            IUIAutomationElement root,
            string pattern,
            TreeScope scope,
            RegexOptions options)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be empty.", nameof(pattern));

            Regex regex = new Regex(pattern, options);
            IUIAutomationElementArray elements = root.FindAll(scope, Anything());

            if (elements == null)
                return null;

            for (int i = 0; i < elements.Length; i++)
            {
                IUIAutomationElement element = elements.GetElement(i);
                string text = GetElementText(element);

                if (!string.IsNullOrEmpty(text) && regex.IsMatch(text))
                    return element;
            }

            return null;
        }

        private static string Normalize(string value)
        {
            if (value == null)
                return string.Empty;

            return value.Replace(" ", string.Empty)
                        .Replace("\r", string.Empty)
                        .Replace("\n", string.Empty)
                        .Replace("\t", string.Empty);
        }

        #endregion TEXT AND VALUE

        #region STATE QUERIES

        /// <summary>
        /// Reads the ToggleState of an element found by Name. Returns null when the
        /// element is absent or exposes no toggle support.
        /// </summary>
        public static ToggleState? GetToggleElementState(BrowserManager browserManager, string name)
        {
            IBrowserController controller = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            IUIAutomationElement element = controller.FindElementByName(webPage, name);

            if (element == null)
                return null;

            return GetToggleState(element);
        }

        /// <summary>
        /// Reads the ToggleState of a specific element: fast property read first, then
        /// the pattern itself.
        /// </summary>
        public static ToggleState? GetToggleState(IUIAutomationElement element)
        {
            if (element == null)
                return null;

            if (ReadBoolProp(element, UIA_PropertyIds.UIA_IsTogglePatternAvailablePropertyId, false))
            {
                object stateObj = element.GetCurrentPropertyValue(
                    UIA_PropertyIds.UIA_ToggleToggleStatePropertyId);

                if (stateObj is int i)
                    return (ToggleState)i;

                if (stateObj is ToggleState ts)
                    return ts;
            }

            try
            {
                IUIAutomationTogglePattern toggle =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId)
                    as IUIAutomationTogglePattern;

                if (toggle != null)
                    return toggle.CurrentToggleState;
            }
            catch (COMException) { }

            return null;
        }

        /// <summary>
        /// Reads a checkbox's checked state through four escalating strategies:
        /// TogglePattern, the ToggleState property, LegacyIAccessible state bits, then
        /// ValuePattern text. Returns null for indeterminate or unreadable.
        ///
        /// TWO FIXES: the nthPosition argument is now actually used -- the original
        /// located the first checkbox, then its next sibling, and ignored the parameter
        /// entirely. And a null element is now returned early instead of being logged
        /// and then dereferenced on the next line.
        /// </summary>
        public static bool? IsCheckboxCheckedNth(BrowserManager browserManager, int nthPosition)
        {
            IBrowserController browserController = browserManager.GetBrowserController();
            IUIAutomationElement webPage = browserManager.GetAndFocusBrowser();

            IUIAutomationElement element = browserController.FindNthElementOfType(
                webPage, UIAControlType.CheckBox, nthPosition);

            if (element == null)
            {
                StatusLog.WriteErrorEntry($"IsCheckboxCheckedNth: no checkbox at position {nthPosition}.");
                return null;
            }

            return IsChecked(element);
        }

        /// <summary>
        /// Reads a specific element's checked state.
        /// </summary>
        public static bool? IsChecked(IUIAutomationElement element)
        {
            if (element == null)
                return null;

            // 1) TogglePattern
            try
            {
                IUIAutomationTogglePattern toggle =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId)
                    as IUIAutomationTogglePattern;

                if (toggle != null)
                {
                    ToggleState state = toggle.CurrentToggleState;

                    if (state == ToggleState.ToggleState_On) return true;
                    if (state == ToggleState.ToggleState_Off) return false;

                    return null;   // Indeterminate
                }
            }
            catch (COMException) { }

            // 2) Property read, ignoring the default when unsupported
            object stateObj = element.GetCurrentPropertyValueEx(
                UIA_PropertyIds.UIA_ToggleToggleStatePropertyId, 1);

            if (stateObj is int i)
            {
                if (i == 1) return true;
                if (i == 0) return false;
                return null;
            }

            // 3) LegacyIAccessible state bits
            try
            {
                IUIAutomationLegacyIAccessiblePattern legacy =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_LegacyIAccessiblePatternId)
                    as IUIAutomationLegacyIAccessiblePattern;

                if (legacy != null)
                {
                    uint state = legacy.CurrentState;

                    if ((state & STATE_SYSTEM_CHECKED) != 0) return true;
                    if ((state & STATE_SYSTEM_MIXED) != 0) return null;

                    return false;
                }
            }
            catch (COMException) { }

            // 4) ValuePattern text
            try
            {
                IUIAutomationValuePattern value =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)
                    as IUIAutomationValuePattern;

                if (value != null)
                {
                    string v = (value.CurrentValue ?? string.Empty).Trim();

                    if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v, "checked", StringComparison.OrdinalIgnoreCase) ||
                        v == "1")
                        return true;

                    if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v, "unchecked", StringComparison.OrdinalIgnoreCase) ||
                        v == "0")
                        return false;
                }
            }
            catch (COMException) { }

            return null;
        }

        /// <summary>
        /// Toggles a control via ExpandCollapsePattern and returns the resulting state.
        ///
        /// CHANGED: the state is now re-read after a short settle rather than read
        /// straight off the same pattern object, which could hand back the pre-action
        /// cached value.
        /// </summary>
        public static ExpandCollapseState ToggleExpandCollapse(IUIAutomationElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            IUIAutomationExpandCollapsePattern pattern =
                element.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId)
                as IUIAutomationExpandCollapsePattern;

            if (pattern == null)
                throw new InvalidOperationException("Element does not support ExpandCollapsePattern.");

            try { element.SetFocus(); } catch (COMException) { }

            switch (pattern.CurrentExpandCollapseState)
            {
                case ExpandCollapseState.ExpandCollapseState_Collapsed:
                case ExpandCollapseState.ExpandCollapseState_PartiallyExpanded:
                    pattern.Expand();
                    break;

                case ExpandCollapseState.ExpandCollapseState_Expanded:
                    pattern.Collapse();
                    break;

                case ExpandCollapseState.ExpandCollapseState_LeafNode:
                    // Nothing to expand or collapse.
                    break;
            }

            Thread.Sleep(100);

            IUIAutomationExpandCollapsePattern refreshed =
                element.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId)
                as IUIAutomationExpandCollapsePattern;

            return refreshed != null
                ? refreshed.CurrentExpandCollapseState
                : pattern.CurrentExpandCollapseState;
        }

        /// <summary>
        /// Sets a RangeValuePattern control (slider, spinner) to a value, clamped to the
        /// control's own minimum and maximum.
        ///
        /// SetSliderValue and SetSpinnerValue were identical apart from their exception
        /// text; both now forward here.
        /// </summary>
        public static void SetRangeValue(IUIAutomationElement element, double value)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            IUIAutomationRangeValuePattern range =
                element.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId)
                as IUIAutomationRangeValuePattern;

            if (range == null)
                throw new InvalidOperationException("Element does not support RangeValuePattern.");

            if (range.CurrentIsReadOnly != 0)
                throw new InvalidOperationException("Element is read-only.");

            double clamped = Math.Max(range.CurrentMinimum, Math.Min(range.CurrentMaximum, value));
            range.SetValue(clamped);
        }

        /// <summary>Back-compat alias of SetRangeValue.</summary>
        public static void SetSliderValue(IUIAutomationElement slider, double value)
        {
            SetRangeValue(slider, value);
        }

        /// <summary>Back-compat alias of SetRangeValue.</summary>
        public static void SetSpinnerValue(IUIAutomationElement spinner, double value)
        {
            SetRangeValue(spinner, value);
        }

        #endregion STATE QUERIES

        #region SCROLLING

        /// <summary>
        /// Scrolls an element into view if it supports ScrollItemPattern. Silent no-op
        /// otherwise, so it is safe to call unconditionally.
        /// </summary>
        public static void ScrollIntoViewIfPossible(IUIAutomationElement element)
        {
            if (element == null)
                return;

            try
            {
                IUIAutomationScrollItemPattern scrollItem =
                    element.GetCurrentPattern(UIA_PatternIds.UIA_ScrollItemPatternId)
                    as IUIAutomationScrollItemPattern;

                if (scrollItem != null)
                    scrollItem.ScrollIntoView();
            }
            catch (COMException) { }
        }

        /// <summary>
        /// Scrolls an element into view, then waits until it reports real geometry.
        /// </summary>
        public static void ScrollIntoViewAndWait(IUIAutomationElement element, int timeoutMs = 2000)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            ScrollIntoViewIfPossible(element);

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                tagRECT r = element.CurrentBoundingRectangle;

                if (r.right > r.left && r.bottom > r.top && element.CurrentIsOffscreen == 0)
                    return;

                Thread.Sleep(50);
            }

            stopwatch.Stop();

            // Browsers often keep IsOffscreen true even for clickable elements, so a
            // valid rectangle alone is good enough to proceed.
            tagRECT rect = element.CurrentBoundingRectangle;

            if (rect.right <= rect.left || rect.bottom <= rect.top)
                throw new InvalidOperationException(
                    "Element did not get a valid bounding rectangle after scrolling.");
        }

        #endregion SCROLLING

        #region TABS AND WINDOWS

        // The original file had three overlapping tab-activation methods:
        // ActivateTabByCaption, SwitchToTabByName and FocusTabItemByName, plus
        // FocusTabItem and GetTabItemByName. They now share one core.

        /// <summary>
        /// Activates a browser tab whose caption matches.
        /// </summary>
        /// <param name="partialMatch">True for substring match, false for exact.</param>
        public static bool ActivateTabByCaption(
            IUIAutomationElement browserWindow,
            string caption,
            bool partialMatch = true)
        {
            if (browserWindow == null)
                throw new ArgumentNullException(nameof(browserWindow));

            IUIAutomationElement tab = FindTabItem(browserWindow, caption, partialMatch);

            if (tab == null)
                return false;

            BringToForeground(browserWindow);
            return ActivateTabItem(tab);
        }

        /// <summary>
        /// Activates a browser tab by exact name, throwing when it is absent.
        /// Kept for existing call sites; ActivateTabByCaption is the softer form.
        /// </summary>
        public static void SwitchToTabByName(
            IUIAutomation automation,
            IUIAutomationElement chromeWindow,
            string tabName)
        {
            if (chromeWindow == null)
                throw new ArgumentNullException(nameof(chromeWindow));

            if (string.IsNullOrWhiteSpace(tabName))
                throw new ArgumentNullException(nameof(tabName));

            BringToForeground(chromeWindow);

            IUIAutomationElement tab = FindTabItem(chromeWindow, tabName, partialMatch: false);

            if (tab == null)
                throw new InvalidOperationException($"Tab not found: \"{tabName}\"");

            if (!ActivateTabItem(tab))
                throw new InvalidOperationException("Could not activate the tab item.");

            try { tab.SetFocus(); } catch (COMException) { }
        }

        /// <summary>
        /// Focuses a TabItem inside a document by its header text.
        /// </summary>
        public static void FocusTabItemByName(IUIAutomationElement documentElement, string tabName)
        {
            if (documentElement == null)
                throw new ArgumentNullException(nameof(documentElement));

            if (string.IsNullOrWhiteSpace(tabName))
                throw new ArgumentException("Tab name cannot be empty.", nameof(tabName));

            IUIAutomationElement tabItem = FindTabItem(documentElement, tabName, partialMatch: true);

            if (tabItem == null)
                throw new InvalidOperationException($"No TabItem found with name containing \"{tabName}\".");

            FocusTabItem(tabItem);
        }

        /// <summary>
        /// Drives a TabItem to selected: ScrollItem, then SelectionItem, then a child
        /// with InvokePattern, then SetFocus.
        /// </summary>
        public static void FocusTabItem(IUIAutomationElement tabItem)
        {
            if (tabItem == null)
                throw new ArgumentNullException(nameof(tabItem));

            ScrollIntoViewIfPossible(tabItem);
            Thread.Sleep(50);

            if (ActivateTabItem(tabItem))
                return;

            IUIAutomationElement invokeChild = GetInvokeChild(tabItem);

            if (invokeChild != null)
            {
                try
                {
                    IUIAutomationInvokePattern invoke =
                        invokeChild.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)
                        as IUIAutomationInvokePattern;

                    if (invoke != null)
                    {
                        invoke.Invoke();
                        return;
                    }
                }
                catch (COMException) { }
            }

            try { tabItem.SetFocus(); } catch (COMException) { }
        }

        /// <summary>
        /// Returns true when the given tab is the selected one.
        /// </summary>
        public static bool IsTabSelected(IUIAutomationElement tab)
        {
            if (tab == null)
                return false;

            try
            {
                IUIAutomationSelectionItemPattern selection =
                    tab.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId)
                    as IUIAutomationSelectionItemPattern;

                return selection != null && selection.CurrentIsSelected != 0;
            }
            catch (COMException)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds a Chrome top-level window whose title contains the given text.
        /// </summary>
        public static IUIAutomationElement FindChromeWindowByTitle(
            IUIAutomation automation,
            string partialTitle)
        {
            IUIAutomationElement root = _uia.GetRootElement();

            IUIAutomationElementArray windows = root.FindAll(
                TreeScope.TreeScope_Children,
                ByControlType(UIA_ControlTypeIds.UIA_WindowControlTypeId));

            if (windows == null)
                return null;

            for (int i = 0; i < windows.Length; i++)
            {
                IUIAutomationElement window = windows.GetElement(i);
                string name = SafeGet(() => window.CurrentName, string.Empty);

                if (!string.IsNullOrEmpty(name) &&
                    name.Contains(partialTitle) &&
                    name.Contains("Google Chrome"))
                {
                    return window;
                }
            }

            return null;
        }

        /// <summary>
        /// Restores a minimized window and pulls it to the foreground.
        /// </summary>
        public static void BringToForeground(IUIAutomationElement window)
        {
            if (window == null)
                return;

            try
            {
                IUIAutomationWindowPattern windowPattern =
                    window.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId)
                    as IUIAutomationWindowPattern;

                if (windowPattern != null &&
                    windowPattern.CurrentWindowVisualState == WindowVisualState.WindowVisualState_Minimized)
                {
                    windowPattern.SetWindowVisualState(WindowVisualState.WindowVisualState_Normal);
                    Thread.Sleep(150);
                }
            }
            catch (COMException) { }

            try
            {
                IntPtr handle = (IntPtr)window.CurrentNativeWindowHandle;

                if (handle != IntPtr.Zero)
                    SetForegroundWindow(handle);
            }
            catch (COMException) { }

            try { window.SetFocus(); } catch (COMException) { }
        }

        // -----------------------------------------------------------------
        // Tab internals
        // -----------------------------------------------------------------

        private static IUIAutomationElement FindTabItem(
            IUIAutomationElement root,
            string caption,
            bool partialMatch)
        {
            if (!partialMatch)
            {
                IUIAutomationCondition exact = _uia.CreateAndCondition(
                    ByControlType(UIA_ControlTypeIds.UIA_TabItemControlTypeId),
                    ByName(caption));

                return root.FindFirst(TreeScope.TreeScope_Descendants, exact);
            }

            IUIAutomationElementArray tabs = root.FindAll(
                TreeScope.TreeScope_Descendants,
                ByControlType(UIA_ControlTypeIds.UIA_TabItemControlTypeId));

            if (tabs == null || tabs.Length == 0)
                return null;

            for (int i = 0; i < tabs.Length; i++)
            {
                IUIAutomationElement tab = tabs.GetElement(i);
                string name = SafeGet(() => tab.CurrentName, string.Empty) ?? string.Empty;

                if (name.IndexOf(caption, StringComparison.OrdinalIgnoreCase) >= 0)
                    return tab;
            }

            return null;
        }

        /// <summary>
        /// Selects a tab item: SelectionItem, then Invoke, then a physical click.
        ///
        /// CHANGED: the physical-click branch now actually clicks. The original had its
        /// only statement commented out ('CtrlClickHelpers.Click(tab)') but still
        /// returned true, so a failure to switch tabs was reported as success.
        /// </summary>
        private static bool ActivateTabItem(IUIAutomationElement tab)
        {
            ScrollIntoViewIfPossible(tab);

            try
            {
                IUIAutomationSelectionItemPattern selection =
                    tab.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId)
                    as IUIAutomationSelectionItemPattern;

                if (selection != null)
                {
                    selection.Select();
                    return true;
                }
            }
            catch (COMException) { }

            try
            {
                IUIAutomationInvokePattern invoke =
                    tab.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId)
                    as IUIAutomationInvokePattern;

                if (invoke != null)
                {
                    invoke.Invoke();
                    return true;
                }
            }
            catch (COMException) { }

            try
            {
                ClickElement(tab);
                return true;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"ActivateTabItem: could not activate the tab. {ex.Message}");
                return false;
            }
        }

        private static IUIAutomationElement GetInvokeChild(IUIAutomationElement parent)
        {
            IUIAutomationCondition condition = _uia.CreatePropertyCondition(
                UIA_PropertyIds.UIA_IsInvokePatternAvailablePropertyId, 1);

            return parent.FindFirst(TreeScope.TreeScope_Descendants, condition);
        }

        #endregion TABS AND WINDOWS

        #region DIAGNOSTICS

        /// <summary>
        /// Writes an indented dump of the UIA tree to StatusLog. The fastest way to see
        /// what your code actually sees at runtime, which can differ from the inspector.
        /// </summary>
        public static void DumpTree(IUIAutomationElement root, int maxDepth = 6)
        {
            if (root == null)
            {
                StatusLog.WriteWarningEntry("DumpTree: root is null.");
                return;
            }

            StatusLog.WriteInformationEntry("---- UIA TREE DUMP (begin) ----");
            DumpNode(root, 0, maxDepth);
            StatusLog.WriteInformationEntry("---- UIA TREE DUMP (end) ----");
        }

        private static void DumpNode(IUIAutomationElement element, int depth, int maxDepth)
        {
            if (element == null || depth > maxDepth)
                return;

            StatusLog.WriteInformationEntry(new string(' ', depth * 2) + DescribeElement(element));

            IUIAutomationTreeWalker walker = _uia.ControlViewWalker;
            IUIAutomationElement child = null;

            try { child = walker.GetFirstChildElement(element); }
            catch (COMException) { return; }

            while (child != null)
            {
                DumpNode(child, depth + 1, maxDepth);

                try { child = walker.GetNextSiblingElement(child); }
                catch (COMException) { break; }
            }
        }

        /// <summary>
        /// One-line summary of an element, matching the fields the inspector shows in its
        /// Identification panel.
        /// </summary>
        public static string DescribeElement(IUIAutomationElement element)
        {
            if (element == null)
                return "<null>";

            string name = SafeGet(() => element.CurrentName, string.Empty);
            string automationId = SafeGet(() => element.CurrentAutomationId, string.Empty);
            string className = SafeGet(() => element.CurrentClassName, string.Empty);
            string frameworkId = SafeGet(() => element.CurrentFrameworkId, string.Empty);
            int controlType = SafeGet(() => element.CurrentControlType, 0);
            tagRECT r = SafeGet(() => element.CurrentBoundingRectangle, new tagRECT());
            int offscreen = SafeGet(() => element.CurrentIsOffscreen, 1);
            int enabled = SafeGet(() => element.CurrentIsEnabled, 0);

            return $"{ControlTypeIdToFriendlyName(controlType)} " +
                   $"Name='{name}' AutomationId='{automationId}' Class='{className}' " +
                   $"Framework='{frameworkId}' " +
                   $"Rect={{{r.left},{r.top},{r.right - r.left}x{r.bottom - r.top}}} " +
                   $"Enabled={enabled == 1} Offscreen={offscreen != 0}";
        }

        /// <summary>
        /// Reports what is currently focused, including its ordinal among same-typed
        /// elements in the document -- which is what the FindNthElementOfType helpers
        /// need as input.
        /// </summary>
        public static class UIAFocusedElementInspector
        {
            public sealed class FocusedElementInfo
            {
                public IUIAutomationElement Element { get; private set; }
                public int ControlTypeId { get; private set; }
                public string ControlTypeName { get; private set; }
                public string Name { get; private set; }
                public string AutomationId { get; private set; }
                public string FrameworkId { get; private set; }
                public string ClassName { get; private set; }
                public int IndexAmongSameType { get; private set; }   // 1-based
                public int TotalOfSameType { get; private set; }

                public FocusedElementInfo(
                    IUIAutomationElement element,
                    int controlTypeId,
                    string controlTypeName,
                    string name,
                    string automationId,
                    string frameworkId,
                    string className,
                    int indexAmongSameType,
                    int totalOfSameType)
                {
                    Element = element;
                    ControlTypeId = controlTypeId;
                    ControlTypeName = controlTypeName ?? string.Empty;
                    Name = name ?? string.Empty;
                    AutomationId = automationId ?? string.Empty;
                    FrameworkId = frameworkId ?? string.Empty;
                    ClassName = className ?? string.Empty;
                    IndexAmongSameType = indexAmongSameType;
                    TotalOfSameType = totalOfSameType;
                }

                public override string ToString()
                {
                    return "Focused: '" + Name + "'  Type: " + ControlTypeName + " (" + ControlTypeId + ")  "
                         + "Order: " + IndexAmongSameType + "/" + TotalOfSameType + "  "
                         + "AutomationId: '" + AutomationId + "'  FrameworkId: '" + FrameworkId + "'  "
                         + "ClassName: '" + ClassName + "'";
                }
            }

            public static FocusedElementInfo Get()
            {
                IUIAutomationElement focused = _uia.GetFocusedElement();

                if (focused == null)
                    return null;

                IUIAutomationElement scopeRoot = ResolveDocumentRoot(focused);

                int focusedTypeId = SafeGet(
                    () => focused.CurrentControlType, UIA_ControlTypeIds.UIA_CustomControlTypeId);

                IUIAutomationElementArray ofSameType = scopeRoot.FindAll(
                    TreeScope.TreeScope_Subtree, ByControlType(focusedTypeId));

                int total = (ofSameType != null) ? ofSameType.Length : 0;
                int index = 0;

                if (total > 0)
                {
                    int[] focusedRuntimeId = SafeGetRuntimeId(focused);

                    for (int i = 0; i < total; i++)
                    {
                        IUIAutomationElement candidate = ofSameType.GetElement(i);

                        if (RuntimeIdsEqual(focusedRuntimeId, SafeGetRuntimeId(candidate)))
                        {
                            index = i + 1;
                            break;
                        }
                    }
                }

                return new FocusedElementInfo(
                    focused,
                    focusedTypeId,
                    ControlTypeIdToFriendlyName(focusedTypeId),
                    SafeGet(() => focused.CurrentName, string.Empty),
                    SafeGet(() => focused.CurrentAutomationId, string.Empty),
                    SafeGet(() => focused.CurrentFrameworkId, string.Empty),
                    SafeGet(() => focused.CurrentClassName, string.Empty),
                    index,
                    total);
            }

            /// <summary>
            /// Walks up from the focused element to the highest Document within the same
            /// top-level HWND, falling back to the highest Pane or Window.
            /// </summary>
            private static IUIAutomationElement ResolveDocumentRoot(IUIAutomationElement start)
            {
                IUIAutomationTreeWalker walker = _uia.RawViewWalker;
                IntPtr startHwnd = SafeGet(() => (IntPtr)start.CurrentNativeWindowHandle, IntPtr.Zero);

                IUIAutomationElement best = start;
                IUIAutomationElement lastDocument = null;

                for (IUIAutomationElement current = start; current != null; )
                {
                    best = current;

                    if (SafeGet(() => current.CurrentControlType, 0) ==
                        UIA_ControlTypeIds.UIA_DocumentControlTypeId)
                    {
                        lastDocument = current;
                    }

                    IUIAutomationElement parent = walker.GetParentElement(current);

                    if (LeftTopLevelWindow(parent, startHwnd))
                        break;

                    current = parent;
                }

                if (lastDocument != null)
                    return lastDocument;

                IUIAutomationElement highest = best;

                for (IUIAutomationElement current = best; current != null; )
                {
                    int controlType = SafeGet(() => current.CurrentControlType, 0);

                    if (controlType == UIA_ControlTypeIds.UIA_PaneControlTypeId ||
                        controlType == UIA_ControlTypeIds.UIA_WindowControlTypeId)
                    {
                        highest = current;
                    }

                    IUIAutomationElement parent = walker.GetParentElement(current);

                    if (LeftTopLevelWindow(parent, startHwnd))
                        break;

                    current = parent;
                }

                return highest ?? start;
            }

            private static bool LeftTopLevelWindow(IUIAutomationElement parent, IntPtr startHwnd)
            {
                if (parent == null)
                    return true;

                IntPtr parentHwnd = SafeGet(() => (IntPtr)parent.CurrentNativeWindowHandle, IntPtr.Zero);

                return startHwnd != IntPtr.Zero
                    && parentHwnd != IntPtr.Zero
                    && parentHwnd != startHwnd;
            }
        }

        #endregion DIAGNOSTICS

        #region PROPERTY READERS

        // SafeGet was previously declared twice -- once at class scope and once inside
        // UIAFocusedElementInspector. One copy now serves both.

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch (COMException) { return fallback; }
            catch (NullReferenceException) { return fallback; }
        }

        private static bool ReadBoolProp(IUIAutomationElement element, int propertyId, bool fallback)
        {
            try
            {
                // Always go through the variant/object API rather than a typed accessor.
                object value = element.GetCurrentPropertyValue(propertyId);
                return ConvertToBool(value, fallback);
            }
            catch (COMException) { return fallback; }
            catch (NullReferenceException) { return fallback; }
        }

        private static bool ConvertToBool(object value, bool fallback)
        {
            if (value == null)
                return fallback;

            if (value is bool b)
                return b;

            if (value is int i)
                return i != 0;

            string s = value.ToString();

            bool parsedBool;
            if (bool.TryParse(s, out parsedBool))
                return parsedBool;

            int parsedInt;
            if (int.TryParse(s, out parsedInt))
                return parsedInt != 0;

            return fallback;
        }

        private static int[] SafeGetRuntimeId(IUIAutomationElement element)
        {
            try
            {
                object obj = element.GetRuntimeId();

                int[] asIntArray = obj as int[];
                if (asIntArray != null)
                    return asIntArray;

                Array asArray = obj as Array;
                if (asArray != null && asArray.Length > 0)
                {
                    int[] copy = new int[asArray.Length];

                    for (int i = 0; i < asArray.Length; i++)
                        copy[i] = (int)asArray.GetValue(i);

                    return copy;
                }

                return new int[0];
            }
            catch
            {
                return new int[0];
            }
        }

        private static bool RuntimeIdsEqual(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;

            return true;
        }

        private static string ControlTypeIdToFriendlyName(int id)
        {
            switch (id)
            {
                case UIA_ControlTypeIds.UIA_ButtonControlTypeId: return "Button";
                case UIA_ControlTypeIds.UIA_CheckBoxControlTypeId: return "CheckBox";
                case UIA_ControlTypeIds.UIA_ComboBoxControlTypeId: return "ComboBox";
                case UIA_ControlTypeIds.UIA_CustomControlTypeId: return "Custom";
                case UIA_ControlTypeIds.UIA_DataGridControlTypeId: return "DataGrid";
                case UIA_ControlTypeIds.UIA_DataItemControlTypeId: return "DataItem";
                case UIA_ControlTypeIds.UIA_DocumentControlTypeId: return "Document";
                case UIA_ControlTypeIds.UIA_EditControlTypeId: return "Edit";
                case UIA_ControlTypeIds.UIA_GroupControlTypeId: return "Group";
                case UIA_ControlTypeIds.UIA_HeaderControlTypeId: return "Header";
                case UIA_ControlTypeIds.UIA_HeaderItemControlTypeId: return "HeaderItem";
                case UIA_ControlTypeIds.UIA_HyperlinkControlTypeId: return "Hyperlink";
                case UIA_ControlTypeIds.UIA_ImageControlTypeId: return "Image";
                case UIA_ControlTypeIds.UIA_ListControlTypeId: return "List";
                case UIA_ControlTypeIds.UIA_ListItemControlTypeId: return "ListItem";
                case UIA_ControlTypeIds.UIA_MenuControlTypeId: return "Menu";
                case UIA_ControlTypeIds.UIA_MenuBarControlTypeId: return "MenuBar";
                case UIA_ControlTypeIds.UIA_MenuItemControlTypeId: return "MenuItem";
                case UIA_ControlTypeIds.UIA_PaneControlTypeId: return "Pane";
                case UIA_ControlTypeIds.UIA_RadioButtonControlTypeId: return "RadioButton";
                case UIA_ControlTypeIds.UIA_ScrollBarControlTypeId: return "ScrollBar";
                case UIA_ControlTypeIds.UIA_SliderControlTypeId: return "Slider";
                case UIA_ControlTypeIds.UIA_SpinnerControlTypeId: return "Spinner";
                case UIA_ControlTypeIds.UIA_SplitButtonControlTypeId: return "SplitButton";
                case UIA_ControlTypeIds.UIA_TabControlTypeId: return "Tab";
                case UIA_ControlTypeIds.UIA_TabItemControlTypeId: return "TabItem";
                case UIA_ControlTypeIds.UIA_TextControlTypeId: return "Text";
                case UIA_ControlTypeIds.UIA_ThumbControlTypeId: return "Thumb";
                case UIA_ControlTypeIds.UIA_ToolBarControlTypeId: return "ToolBar";
                case UIA_ControlTypeIds.UIA_TreeControlTypeId: return "Tree";
                case UIA_ControlTypeIds.UIA_TreeItemControlTypeId: return "TreeItem";
                case UIA_ControlTypeIds.UIA_WindowControlTypeId: return "Window";
                default: return "ControlType(" + id + ")";
            }
        }

        #endregion PROPERTY READERS

        #region WIN32 INTEROP AND COORDINATES

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int INPUT_MOUSE = 0;

        // The original declared each of these twice, once plain and once with a '2'
        // suffix, at identical values.
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        // MSAA state bits used by IsChecked.
        private const int STATE_SYSTEM_CHECKED = 0x10;
        private const int STATE_SYSTEM_MIXED = 0x20;

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Converts a screen X coordinate into the 0..65535 absolute space, measured
        /// across the whole virtual desktop rather than the primary monitor.
        /// </summary>
        private static double NormalizeX(double x)
        {
            int left, top, width, height;
            GetVirtualDesktopBounds(out left, out top, out width, out height);
            return ((x - left) * 65535.0) / Math.Max(1, width - 1);
        }

        private static double NormalizeY(double y)
        {
            int left, top, width, height;
            GetVirtualDesktopBounds(out left, out top, out width, out height);
            return ((y - top) * 65535.0) / Math.Max(1, height - 1);
        }

        private static void GetVirtualDesktopBounds(out int left, out int top, out int width, out int height)
        {
            left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        }

        #endregion WIN32 INTEROP AND COORDINATES

        /// <summary>
        /// Finds the first element with the given exact accessible name that is actually visible on screen.
        /// Useful when a name is shared by a visible control and a hidden/off-screen element elsewhere on the
        /// page (e.g. a menu item that shares text with a toolbar icon).
        /// </summary>
        public static IUIAutomationElement GetVisibleElementByName(IUIAutomationElement root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
        
            var automation = new CUIAutomation();
            var nameCondition = automation.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, name);
            IUIAutomationElementArray matches = root.FindAll(TreeScope.TreeScope_Descendants, nameCondition);
        
            for (int i = 0; i < matches.Length; i++)
            {
                IUIAutomationElement candidate = matches.GetElement(i);
                if (IsVisiblyRendered(candidate))
                    return candidate;
            }
            return null;
        }
        
        /// <summary>
        /// Given any cell within a grid row, checks the toggle state of the Nth checkbox found in that row
        /// (1-based).  Useful for grids like Halo's Add Assay dialog, where the first column's checkbox
        /// indicates whether the row is already added to the case.
        /// </summary>
        public static bool? IsNthCheckboxCheckedInRow(IUIAutomationElement rowCell, int checkboxPosition = 1)
        {
            if (rowCell == null) return null;
        
            IUIAutomationElement row = GetParent(rowCell);
            IUIAutomationElement checkbox = GetNthChildByType(row, UIAControlType.CheckBox, checkboxPosition);
            if (checkbox == null) return null;
        
            try
            {
                var togglePattern = checkbox.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId) as IUIAutomationTogglePattern;
                if (togglePattern != null)
                    return togglePattern.CurrentToggleState == ToggleState.ToggleState_On;
            }
            catch { }
            return null;
        }
        
        /// <summary>
        /// Same as GetElementMatchingRegex, but skips a specific element -- useful when a search/filter
        /// textbox on the page currently displays the same text you're searching for, and would otherwise
        /// match before the actual result you want.
        /// </summary>
        public static IUIAutomationElement GetElementMatchingRegexExcluding(
            IUIAutomationElement root,
            string pattern,
            IUIAutomationElement exclude,
            RegexOptions options = RegexOptions.IgnoreCase)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be empty.", nameof(pattern));
        
            var automation = new CUIAutomation();
            Regex regex = new Regex(pattern, options);
        
            var elements = root.FindAll(TreeScope.TreeScope_Descendants, automation.CreateTrueCondition());
        
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements.GetElement(i);
        
                if (exclude != null && automation.CompareElements(element, exclude) != 0)
                    continue;
        
                string text = GetElementText(element);
                if (!string.IsNullOrEmpty(text) && regex.IsMatch(text))
                {
                    return element;
                }
            }
        
            return null;
        }
        
#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
