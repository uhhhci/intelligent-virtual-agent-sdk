using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace IVH.Core.EditorScripts.Setup
{
    /// <summary>
    /// Runs once on editor load and prompts the user to install required external packages that
    /// Unity Package Manager can't auto-resolve from the SDK's <c>package.json</c> — specifically
    /// the Oculus lip-sync git dependency. Unity intentionally does not transitively fetch git-URL
    /// packages declared in a published manifest (security guardrail), so the SDK has to nudge the
    /// user to install them via a one-click <c>Client.Add(gitUrl)</c> instead.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="SessionState"/> to skip the prompt for the rest of this Unity launch once
    /// the user has dismissed it — so the dialog doesn't re-pop on every script recompile, but it
    /// does come back the next time the user re-launches Unity (until they install the package).
    /// </remarks>
    [InitializeOnLoad]
    public static class IVADependencyBootstrap
    {
        /// <summary>
        /// Required external packages that ship as git URLs (not Unity registry / OpenUPM).
        /// Mirrors the relevant rows in <see cref="IVASetupWizard"/>'s Deps list; duplicated here
        /// so the bootstrap stays decoupled from the wizard's private internals.
        /// </summary>
        private static readonly (string id, string label, string installSource)[] RequiredExternal = new[]
        {
            ("com.oculus.unity.integration.lip-sync", "Oculus Lip Sync", "https://github.com/Trisgram/com.oculus.unity.integration.lip-sync.git"),
        };

        private const string SessionPromptedKey = "IVA.DependencyBootstrap.PromptedThisSession";

        static IVADependencyBootstrap()
        {
            // Defer the check until the editor finishes its current compile / asset import cycle.
            // Client.List returns no useful data while compilation is in flight.
            EditorApplication.delayCall += RunCheck;
        }

        private static void RunCheck()
        {
            if (SessionState.GetBool(SessionPromptedKey, false)) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                // Retry on the next editor frame once compilation settles.
                EditorApplication.delayCall += RunCheck;
                return;
            }

            // includeIndirectDependencies=true so packages pulled in via the SDK's own
            // package.json#dependencies (Animation Rigging, Newtonsoft JSON) are visible here. With the
            // flag off, Client.List only returns direct user-project deps and would report them missing.
            ListRequest listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update += WaitForList;

            void WaitForList()
            {
                if (!listRequest.IsCompleted) return;
                EditorApplication.update -= WaitForList;

                if (listRequest.Status != StatusCode.Success)
                {
                    Debug.LogWarning($"[IVA SDK] Could not list installed packages to check dependencies: {listRequest.Error?.message}");
                    return;
                }

                var missing = new List<(string id, string label, string installSource)>();
                foreach (var dep in RequiredExternal)
                {
                    bool found = false;
                    foreach (var pkg in listRequest.Result)
                    {
                        if (pkg.name == dep.id) { found = true; break; }
                    }
                    if (!found) missing.Add(dep);
                }
                if (missing.Count == 0) return;

                // Suppress re-prompts for the remainder of this Unity session. The user gets one
                // chance per launch — clearing this key only requires restarting the editor.
                SessionState.SetBool(SessionPromptedKey, true);

                string body = "The IVA SDK requires the following external package(s) which are not installed:\n";
                foreach (var dep in missing) body += $"\n  • {dep.label}";
                body += "\n\nUnity Package Manager doesn't auto-install git-URL dependencies — they need a one-click install via this dialog or the Setup Wizard.";

                int choice = EditorUtility.DisplayDialogComplex(
                    "IVA SDK — Missing Required Dependency",
                    body,
                    "Install Now",
                    "Open Setup Wizard",
                    "Dismiss");

                switch (choice)
                {
                    case 0: // Install Now
                        foreach (var dep in missing)
                        {
                            Client.Add(dep.installSource);
                            Debug.Log($"[IVA SDK] Installing {dep.id} from {dep.installSource}. Watch the Package Manager window for progress.");
                        }
                        break;
                    case 1: // Open Setup Wizard
                        IVASetupWizard.Open();
                        break;
                    default: // Dismiss
                        Debug.LogWarning($"[IVA SDK] {missing.Count} required external package(s) still missing. Open IVA SDK → Setup Wizard to install when ready.");
                        break;
                }
            }
        }
    }
}
