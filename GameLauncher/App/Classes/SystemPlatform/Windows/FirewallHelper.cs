﻿using GameLauncher.App.Classes.LauncherCore.FileReadWrite;
using GameLauncher.App.Classes.Logger;
using GameLauncher.App.Classes.SystemPlatform.Linux;
using NetFwTypeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WindowsFirewallHelper;
using WindowsFirewallHelper.Exceptions;
using WindowsFirewallHelper.FirewallRules;

namespace GameLauncher.App.Classes.SystemPlatform.Windows
{
    class FirewallHelper
    {
        public static void DoesRulesExist(bool removeFirewallRule, bool firstTimeRun, string nameOfApp, string localOfApp, string groupKey, string description, FirewallProtocol protocol)
        {
            if (FirewallManager.IsServiceRunning == true && !DetectLinux.LinuxDetected())
            {
                if (FirewallStatus() == true)
                {
                    CheckIfRuleExists(removeFirewallRule, firstTimeRun, nameOfApp, localOfApp, groupKey, description, FirewallDirection.Inbound, protocol, FirewallDirection.Inbound.ToString());
                    CheckIfRuleExists(removeFirewallRule, firstTimeRun, nameOfApp, localOfApp, groupKey, description, FirewallDirection.Outbound, protocol, FirewallDirection.Outbound.ToString());
                }
                else
                {
                    Log.Warning("WINDOWS FIREWALL: Turned Off [Not by Launcher]");
                }
            }
            else if (FirewallManager.IsServiceRunning == false && !DetectLinux.LinuxDetected())
            {
                Log.Warning("WINDOWS FIREWALL: Service is Stopped [Not by Launcher]");
            }
            else if (DetectLinux.LinuxDetected())
            {
                Log.Warning("WINDOWS FIREWALL: Not Supported On Linux");
            }
            else
            {
                Log.Error("WINDOWS FIREWALL: Unknown Error Had Occured -> Check System Software");
            }
        }

        public static void CheckIfRuleExists(bool removeFirewallRule, bool firstTimeRun, string nameOfApp, string localOfApp, string groupKey, string description, FirewallDirection direction, FirewallProtocol protocol, string firewallLogNote)
        {
            /* Remove Firewall Rules */
            if (removeFirewallRule == true && firstTimeRun == false)
            {
                RemoveRules(nameOfApp, firewallLogNote);
            }
            /* Add Firewall Rules */
            else if (removeFirewallRule == false && firstTimeRun == true)
            {
                AddApplicationRule(nameOfApp, localOfApp, groupKey, description, direction, protocol, firewallLogNote);
            }
            /* Removes a Specific Rule from Firewall (When switching locations) */
            else if (removeFirewallRule == true && firstTimeRun == true)
            {
                if (RuleExist(nameOfApp) == true)
                {
                    RemoveRules(nameOfApp, firewallLogNote);
                    Log.Info("WINDOWS FIREWALL: Found " + nameOfApp + " {" + firewallLogNote + "} In Firewall");
                }
                else if (RuleExist(nameOfApp) == false)
                {
                    AddApplicationRule(nameOfApp, localOfApp, groupKey, description, direction, protocol, firewallLogNote);
                }
            }
            else if (removeFirewallRule == false && firstTimeRun == false)
            {
                Log.Info("WINDOWS FIREWALL: Already Exlcuded " + nameOfApp + " {" + firewallLogNote + "}");
            }

            else
            {
                Log.Error("WINDOWS FIREWALL: Firewall Error - Check With Visual Studio for Error Debuging");
            }
        }

        public static void AddApplicationRule(string nameOfApp, string localOfApp, string groupKey, string description, FirewallDirection direction, FirewallProtocol protocol, string firewallLogNote) 
        {
            try
            {
                Log.Info("WINDOWS FIREWALL: Supported Firewall Found");
                var rule = new FirewallWASRuleWin8(localOfApp, FirewallAction.Allow, direction, FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public)
                {
                    ApplicationName = localOfApp,
                    Name = nameOfApp,
                    Grouping = groupKey,
                    Description = description,
                    NetworkInterfaceTypes = NetworkInterfaceTypes.Lan | NetworkInterfaceTypes.RemoteAccess |
                                     NetworkInterfaceTypes.Wireless,
                    Protocol = protocol
                };

                if (direction == FirewallDirection.Inbound)
                {
                    rule.EdgeTraversalOptions = EdgeTraversalAction.Allow;
                }

                FirewallManager.Instance.Rules.Add(rule);
                Log.Info("WINDOWS FIREWALL: Finished Adding " + nameOfApp + " to Firewall! {" + firewallLogNote + "}");
            }
            catch (FirewallWASNotSupportedException Error)
            {
                Log.Error("WINDOWS FIREWALL: " + Error.Message);
                AddDefaultApplicationRule(nameOfApp, localOfApp, direction, protocol, firewallLogNote);
            }
        }

        private static void AddDefaultApplicationRule(string nameOfApp, string localOfApp, FirewallDirection direction, FirewallProtocol protocol, string firewallLogNote)
        {
            try
            {
                Log.Warning("WINDOWS FIREWALL: Falling back to 'LegacyStandard'");
                var defaultRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    nameOfApp,
                    FirewallAction.Allow,
                    localOfApp, protocol);

                defaultRule.Direction = direction;

                FirewallManager.Instance.Rules.Add(defaultRule);
                Log.Warning("WINDOWS FIREWALL: Finished Adding " + nameOfApp + " to Firewall! {" + firewallLogNote + "}");
            }
            catch (FirewallWASNotSupportedException Error)
            {
                Log.Error("WINDOWS FIREWALL: " + Error.Message);
            }
        }

        public static void RemoveRules(string nameOfApp, string firewallLogNote)
        {
            var myRule = FindRules(nameOfApp).ToArray();
            foreach (var rule in myRule)
                try
                {
                    Log.Warning("WINDOWS FIREWALL: Removed " + nameOfApp + " {" + firewallLogNote + "} From Firewall!");
                    FirewallManager.Instance.Rules.Remove(rule);
                }
                catch (Exception ex)
                {
                    Log.Error("WINDOWS FIREWALL: " + ex.Message);
                }
        }

        public static bool RuleExist(string nameOfApp)
        {
            if (DetectLinux.LinuxDetected())
            {
                return true;
            }
            else
            {
                return FindRules(nameOfApp).Any();
            }
        }

        public static IEnumerable<IFirewallRule> FindRules(string nameOfApp)
        {
            try
            {
                if (FirewallWAS.IsSupported == true && FirewallWASRuleWin7.IsSupported == true)
                    return FirewallManager.Instance.Rules.Where(r => string.Equals(r.Name, nameOfApp,
                        StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            catch 
            {
                return null;
            }

            return null;
        }

        /* Checks if Windows Firewall is Enabled or not from a System Level */
        public static bool FirewallStatus()
        {
            bool FirewallEnabled;

            if (DetectLinux.LinuxDetected())
            {
                FirewallEnabled = false;
            }
            else
            {
                try
                {
                    Type NetFwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
                    INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);

                    FirewallEnabled = mgr.LocalPolicy.CurrentProfile.FirewallEnabled;
                }
                catch
                {
                    FirewallEnabled = false;
                }
            }

            return FirewallEnabled;
        }
    }

    class FirewallFunctions
    {
        public static void GameFiles()
        {
            try
            {
                if (FirewallManager.IsServiceRunning == true && FirewallHelper.FirewallStatus() == true)
                {
                    bool removeFirewallRule = false;
                    bool firstTimeRun = false;

                    string nameOfGame = "SBRW - Game";
                    string localOfGame = FileSettingsSave.GameInstallation + "\\nfsw.exe";

                    string groupKeyGame = "Need for Speed: World";
                    string descriptionGame = groupKeyGame;

                    if (FileSettingsSave.FirewallGameStatus == "Not Excluded" || FileSettingsSave.FirewallGameStatus == "Turned Off" || FileSettingsSave.FirewallGameStatus == "Service Stopped" || FileSettingsSave.FirewallGameStatus == "Unknown")
                    {
                        firstTimeRun = true;
                        FileSettingsSave.FirewallGameStatus = "Excluded";
                    }
                    else if (FileSettingsSave.FirewallGameStatus == "Reset")
                    {
                        removeFirewallRule = true;
                        FileSettingsSave.FirewallGameStatus = "Not Excluded";
                    }

                    /* Inbound & Outbound */
                    FirewallHelper.DoesRulesExist(removeFirewallRule, firstTimeRun, nameOfGame, localOfGame, groupKeyGame, descriptionGame, FirewallProtocol.Any);
                }
                else if (FirewallManager.IsServiceRunning == true && FirewallHelper.FirewallStatus() == false)
                {
                    FileSettingsSave.FirewallGameStatus = "Turned Off";
                }
                else
                {
                    FileSettingsSave.FirewallGameStatus = "Service Stopped";
                }

            }
            catch (Exception error)
            {
                Log.Error("FIREWALL: " + error.Message);
                FileSettingsSave.FirewallGameStatus = "Error";
            }

            FileSettingsSave.SaveSettings();
        }

        public static void Launcher()
        {
            try
            {
                if (FirewallManager.IsServiceRunning == true && FirewallHelper.FirewallStatus() == true)
                {
                    string nameOfLauncher = "SBRW - Game Launcher";
                    string localOfLauncher = Assembly.GetEntryAssembly().Location;

                    string nameOfUpdater = "SBRW - Game Launcher Updater";
                    string localOfUpdater = Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "GameLauncherUpdater.exe");

                    string groupKeyLauncher = "Game Launcher for Windows";
                    string descriptionLauncher = "Soapbox Race World";

                    bool removeFirewallRule = false;
                    bool firstTimeRun = false;

                    if (FileSettingsSave.FirewallLauncherStatus == "Not Excluded" || FileSettingsSave.FirewallLauncherStatus == "Turned Off" || FileSettingsSave.FirewallLauncherStatus == "Service Stopped" || FileSettingsSave.FirewallLauncherStatus == "Unknown")
                    {
                        firstTimeRun = true;
                        FileSettingsSave.FirewallLauncherStatus = "Excluded";
                    }
                    else if (FileSettingsSave.FirewallLauncherStatus == "Reset")
                    {
                        removeFirewallRule = true;
                        FileSettingsSave.FirewallLauncherStatus = "Not Excluded";
                    }

                    /* Inbound & Outbound */
                    FirewallHelper.DoesRulesExist(removeFirewallRule, firstTimeRun, nameOfLauncher, localOfLauncher, groupKeyLauncher, descriptionLauncher, FirewallProtocol.Any);
                    FirewallHelper.DoesRulesExist(removeFirewallRule, firstTimeRun, nameOfUpdater, localOfUpdater, groupKeyLauncher, descriptionLauncher, FirewallProtocol.Any);
                }
                else if (FirewallManager.IsServiceRunning == true && FirewallHelper.FirewallStatus() == false)
                {
                    FileSettingsSave.FirewallLauncherStatus = "Turned Off";
                }
                else
                {
                    FileSettingsSave.FirewallLauncherStatus = "Service Stopped";
                }
            }
            catch (Exception error)
            {
                Log.Error("FIREWALL: " + error.Message);
                FileSettingsSave.FirewallLauncherStatus = "Error";
            }

            FileSettingsSave.SaveSettings();
        }
    }
}
